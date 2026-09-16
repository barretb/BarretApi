using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;

namespace BarretApi.Core.Services;

/// <summary>Versioned management of scheduled posts. No operation publishes to a provider.</summary>
public sealed class ScheduledPostManagementService(
	IScheduledSocialPostRepository repository,
	IEnumerable<ISocialPlatformClient> platformClients,
	TimeProvider clock)
{
	private readonly IScheduledSocialPostRepository _repository = repository;
	private readonly HashSet<string> _platforms = platformClients.Select(p => p.PlatformName).ToHashSet(StringComparer.OrdinalIgnoreCase);
	private readonly TimeProvider _clock = clock;

	public Task<PostManagementResult> UpdateAsync(string id, UpdateScheduledPostCommand command, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		return MutateAsync(id, command.Version, record =>
		{
			if (record.Status != ScheduledPostStatus.Pending || record.AttemptCount != 0 || record.DeliveryResults.Count > 0 || record.DeliveryConfirmations.Count > 0)
			{
				return "Only pending posts that have never been attempted can be edited.";
			}

			ValidateEdit(record, command);
			record.Text = command.Text ?? record.Text;
			record.Hashtags = command.Hashtags ?? record.Hashtags;
			record.TargetPlatforms = command.Platforms is null ? record.TargetPlatforms : NormalizePlatforms(command.Platforms);
			record.ImageUrls = command.Images ?? record.ImageUrls;
			record.UploadedImages = command.RemoveUploadedImages ? [] : record.UploadedImages;
			record.AutoThread = command.AutoThread ?? record.AutoThread;
			record.ScheduledForUtc = command.ScheduledForUtc?.ToUniversalTime() ?? record.ScheduledForUtc;
			record.LastManagementAction = "Updated";
			record.ManagementNote = null;
			return null;
		}, ct);
	}

	public Task<PostManagementResult> CancelAsync(string id, CancelScheduledPostCommand command, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		ValidateNote(command.Note);
		return MutateAsync(id, command.Version, record =>
		{
			if (record.Status != ScheduledPostStatus.Pending)
			{
				return "Only pending posts can be cancelled. Processing and completed posts cannot be cancelled.";
			}

			record.Status = ScheduledPostStatus.Cancelled;
			record.LeaseExpiresAtUtc = null;
			record.LastManagementAction = "Cancelled";
			record.ManagementNote = command.Note.Trim();
			return null;
		}, ct);
	}

	public Task<PostManagementResult> ReconcileAsync(string id, ReconcileScheduledPostCommand command, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		ValidateNote(command.Note);
		ValidateConfirmations(command.PublishedDeliveries);
		return MutateAsync(id, command.Version, record =>
		{
			if (record.Status is not (ScheduledPostStatus.NeedsReview or ScheduledPostStatus.Failed))
			{
				return "Only failed posts or posts needing review can be reconciled.";
			}

			var targets = Targets(record);
			if (command.PublishedDeliveries.Any(d => !targets.Contains(d.Platform, StringComparer.OrdinalIgnoreCase)
				|| record.DeliveryResults.Any(r => SamePlatform(r.Platform, d.Platform) && r.Success)))
			{
				return "Each confirmation must identify an unresolved target platform; successful receipts cannot be overwritten.";
			}

			ApplyConfirmations(record, command, targets);
			return null;
		}, ct);
	}

	public Task<PostManagementResult> RetryAsync(string id, RetryScheduledPostCommand command, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		ValidateNote(command.Note);
		ArgumentNullException.ThrowIfNull(command.Platforms);
		if (!command.ConfirmNotPublished || command.Platforms.Count == 0
			|| command.Platforms.Any(string.IsNullOrWhiteSpace)
			|| command.Platforms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != command.Platforms.Count)
		{
			throw new ArgumentException("Specify distinct remaining platforms and confirm that none of their posts were published.");
		}

		if (command.ScheduledForUtc.HasValue && command.ScheduledForUtc.Value <= _clock.GetUtcNow())
		{
			throw new ArgumentException("ScheduledFor must be in the future when supplied.");
		}

		return MutateAsync(id, command.Version, record => PrepareRetry(record, command), ct);
	}

	private string? PrepareRetry(ScheduledSocialPostRecord record, RetryScheduledPostCommand command)
	{
		if (record.Status is not (ScheduledPostStatus.Failed or ScheduledPostStatus.NeedsReview))
		{
			return "Only failed posts or posts needing review can be retried.";
		}

		var targets = Targets(record);
		var remaining = targets.Where(p => !record.DeliveryResults.Any(r => SamePlatform(r.Platform, p) && r.Success)).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!remaining.SetEquals(command.Platforms) || remaining.Count == 0)
		{
			return "Retry must include exactly all unresolved target platforms. Confirm published deliveries first.";
		}

		if (record.DeliveryResults.Any(r => remaining.Contains(r.Platform) && HasPublishedEvidence(r)))
		{
			return "A remaining platform has published posts or thread segments. Complete and reconcile it instead of replaying it.";
		}

		record.TargetPlatforms = targets;
		record.Status = ScheduledPostStatus.Pending;
		record.DeliveryTrackingEnabled = true;
		record.ScheduledForUtc = command.ScheduledForUtc?.ToUniversalTime() ?? _clock.GetUtcNow();
		record.LeaseExpiresAtUtc = null;
		record.LastErrorCode = null;
		record.LastErrorMessage = null;
		record.LastManagementAction = "RetryQueued";
		record.ManagementNote = command.Note.Trim();
		return null;
	}

	private void ApplyConfirmations(ScheduledSocialPostRecord record, ReconcileScheduledPostCommand command, IReadOnlyList<string> targets)
	{
		var now = _clock.GetUtcNow();
		foreach (var delivery in command.PublishedDeliveries)
		{
			var platform = targets.Single(p => SamePlatform(p, delivery.Platform));
			var previous = record.DeliveryResults.SingleOrDefault(r => SamePlatform(r.Platform, platform));
			record.DeliveryResults.RemoveAll(r => SamePlatform(r.Platform, platform));
			record.DeliveryResults.Add(new PlatformPostResult
			{
				Platform = platform,
				Success = true,
				PostId = delivery.PostId,
				PostUrl = delivery.PostUrl,
				ThreadResults = previous?.ThreadResults
			});
			record.DeliveryConfirmations.RemoveAll(c => SamePlatform(c.Platform, platform));
			record.DeliveryConfirmations.Add(new PostDeliveryConfirmation(platform, delivery.PostId, delivery.PostUrl, now, command.Note.Trim()));
		}

		record.TargetPlatforms = targets;
		var complete = targets.All(p => record.DeliveryResults.Any(r => SamePlatform(r.Platform, p) && r.Success));
		record.Status = complete ? ScheduledPostStatus.Published : ScheduledPostStatus.NeedsReview;
		record.PublishedAtUtc = complete ? now : null;
		record.LastErrorCode = complete ? null : "DELIVERY_UNCERTAIN";
		record.LastErrorMessage = complete ? null : "Resolve the remaining deliveries before retrying.";
		record.LeaseExpiresAtUtc = null;
		record.LastManagementAction = "Reconciled";
		record.ManagementNote = command.Note.Trim();
	}

	private async Task<PostManagementResult> MutateAsync(
		string id, string version, Func<ScheduledSocialPostRecord, string?> mutate, CancellationToken ct)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(version);
		if (version == "*")
		{
			throw new ArgumentException("Use the exact version returned by the details endpoint.");
		}

		var record = await _repository.GetByIdAsync(id, ct);
		if (record is null)
		{
			return new(PostManagementOutcome.NotFound);
		}

		if (!string.Equals(record.Version, version, StringComparison.Ordinal))
		{
			return new(PostManagementOutcome.Conflict, Message: "The post changed. Reload its details before trying again.");
		}

		var error = mutate(record);
		if (error is not null)
		{
			return new(PostManagementOutcome.Conflict, Message: error);
		}

		record.UpdatedAtUtc = _clock.GetUtcNow();
		return await _repository.TryUpdateAsync(record, ct)
			? new(PostManagementOutcome.Success, record)
			: new(PostManagementOutcome.Conflict, Message: "The post changed while saving. Reload its details.");
	}

	private void ValidateEdit(ScheduledSocialPostRecord record, UpdateScheduledPostCommand command)
	{
		if (command.Text is null && command.Hashtags is null && command.Platforms is null && command.Images is null
			&& command.AutoThread is null && command.ScheduledForUtc is null && !command.RemoveUploadedImages)
		{
			throw new ArgumentException("Provide at least one field to change.");
		}

		var text = command.Text ?? record.Text;
		var images = command.Images ?? record.ImageUrls;
		var uploadedCount = command.RemoveUploadedImages ? 0 : record.UploadedImages.Count;
		if (text.Length > 10_000 || (string.IsNullOrWhiteSpace(text) && images.Count + uploadedCount == 0))
		{
			throw new ArgumentException("Text must be at most 10,000 characters and is required when no images remain.");
		}

		if (images.Count + uploadedCount > 4 || images.Any(i => i is null || i.Url is null || i.Url.Length > 2_048 || !IsHttpUrl(i.Url)
			|| string.IsNullOrWhiteSpace(i.AltText) || i.AltText.Length > 1_500))
		{
			throw new ArgumentException("Use up to four images with HTTP(S) URLs and alt text of at most 1,500 characters.");
		}

		var hashtags = command.Hashtags ?? record.Hashtags;
		if (hashtags.Count > 100 || hashtags.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 100 || t.Any(char.IsWhiteSpace)))
		{
			throw new ArgumentException("Use up to 100 hashtags, each at most 100 characters without whitespace.");
		}

		if (command.Platforms is not null)
		{
			NormalizePlatforms(command.Platforms);
		}

		if (command.ScheduledForUtc.HasValue && command.ScheduledForUtc.Value <= _clock.GetUtcNow())
		{
			throw new ArgumentException("ScheduledFor must be in the future.");
		}
	}

	private IReadOnlyList<string> NormalizePlatforms(IReadOnlyList<string> platforms)
	{
		if (platforms.Count == 0 || platforms.Any(p => string.IsNullOrWhiteSpace(p) || !_platforms.Contains(p))
			|| platforms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != platforms.Count)
		{
			throw new ArgumentException("Specify distinct configured platforms.");
		}

		return platforms.Select(p => p.ToLowerInvariant()).ToList();
	}

	private IReadOnlyList<string> Targets(ScheduledSocialPostRecord record)
	{
		// Legacy records omitted explicit targets. Reconciliation requires covering today's configured set.
		return record.TargetPlatforms.Count > 0
			? record.TargetPlatforms.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
			: _platforms.Order().ToList();
	}

	private static void ValidateConfirmations(IReadOnlyList<ConfirmedPostDelivery> deliveries)
	{
		ArgumentNullException.ThrowIfNull(deliveries);
		if (deliveries.Count is < 1 or > 3 || deliveries.Any(d => d is null || string.IsNullOrWhiteSpace(d.Platform)
			|| string.IsNullOrWhiteSpace(d.PostId) || d.PostId.Length > 1_000
			|| (d.PostUrl is not null && (d.PostUrl.Length > 2_048 || !IsHttpUrl(d.PostUrl))))
			|| deliveries.Select(d => d.Platform).Distinct(StringComparer.OrdinalIgnoreCase).Count() != deliveries.Count)
		{
			throw new ArgumentException("Provide distinct published deliveries with post IDs and optional HTTP(S) post URLs.");
		}
	}

	private static void ValidateNote(string note)
	{
		if (string.IsNullOrWhiteSpace(note) || note.Length > 1_000)
		{
			throw new ArgumentException("Provide a note of 1–1,000 characters explaining the action.");
		}
	}

	private static bool HasPublishedEvidence(PlatformPostResult result)
	{
		return result.Success || !string.IsNullOrWhiteSpace(result.PostId) || !string.IsNullOrWhiteSpace(result.PostUrl)
			|| result.ThreadResults?.Any(HasPublishedEvidence) == true;
	}

	private static bool SamePlatform(string first, string second)
	{
		return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsHttpUrl(string? value)
	{
		return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
	}
}
