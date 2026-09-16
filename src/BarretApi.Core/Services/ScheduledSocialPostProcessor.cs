using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

public sealed class ScheduledSocialPostProcessor(
	IScheduledSocialPostRepository scheduledSocialPostRepository,
	SocialPostService socialPostService,
	IScheduledPostImageStore scheduledPostImageStore,
	IOptions<ScheduledSocialPostOptions> options,
	ILogger<ScheduledSocialPostProcessor> logger,
	IEmailNotificationService? emailNotificationService = null)
	: IScheduledSocialPostProcessor
{
	private readonly IScheduledSocialPostRepository _scheduledSocialPostRepository = scheduledSocialPostRepository;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly IScheduledPostImageStore _scheduledPostImageStore = scheduledPostImageStore;
	private readonly ScheduledSocialPostOptions _options = options.Value;
	private readonly ILogger<ScheduledSocialPostProcessor> _logger = logger;
	private readonly IEmailNotificationService? _emailNotificationService = emailNotificationService;

	public async Task<ScheduledPostProcessingSummary> ProcessDueAsync(
		int? maxCount,
		CancellationToken cancellationToken = default)
	{
		var startedAt = DateTimeOffset.UtcNow;
		var runId = $"sched-run-{startedAt:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
		var effectiveMax = Math.Clamp(maxCount ?? _options.MaxBatchSize, 1, 1_000);

		var dueRecords = await _scheduledSocialPostRepository.GetDueForProcessingAsync(
			startedAt,
			effectiveMax,
			cancellationToken);

		var succeededCount = 0;
		var failedCount = 0;
		var skippedCount = 0;
		var attemptedCount = 0;
		var failures = new List<ScheduledPostFailureDetails>();

		foreach (var dueRecord in dueRecords)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ScheduledSocialPostRecord? claimed = null;
			try
			{
				claimed = await _scheduledSocialPostRepository.TryClaimAsync(
					dueRecord.ScheduledPostId, DateTimeOffset.UtcNow, cancellationToken);
				if (claimed is null)
				{
					skippedCount++;
					continue;
				}

				attemptedCount++;
				if (claimed.Status == ScheduledPostStatus.Processing)
				{
					await ProcessClaimAsync(claimed, cancellationToken);
				}

				if (claimed.Status == ScheduledPostStatus.Published)
				{
					succeededCount++;
					continue;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Scheduled post {ScheduledPostId} failed; continuing batch", dueRecord.ScheduledPostId);
				// Storage failures leave the durable claim for expiry/reconciliation.
				claimed ??= dueRecord;
				claimed.LastErrorCode = "PROCESSING_ERROR";
				claimed.LastErrorMessage = "Processing could not complete. Check logs and delivery state.";
			}

			failedCount++;
			failures.Add(new ScheduledPostFailureDetails
			{
				ScheduledPostId = claimed.ScheduledPostId,
				ScheduledForUtc = claimed.ScheduledForUtc,
				Platforms = claimed.TargetPlatforms,
				ErrorCode = claimed.LastErrorCode ?? "PUBLISH_FAILED",
				ErrorMessage = claimed.LastErrorMessage ?? "One or more deliveries failed.",
				AttemptedAtUtc = DateTimeOffset.UtcNow
			});

			try
			{
				await NotifyScheduledPostFailureAsync(claimed, claimed.DeliveryResults,
					claimed.LastErrorCode ?? "PUBLISH_FAILED",
					claimed.LastErrorMessage ?? "One or more deliveries failed.", cancellationToken);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogError(ex, "Failed to notify for scheduled post {ScheduledPostId}", claimed.ScheduledPostId);
			}
		}

		var summary = new ScheduledPostProcessingSummary
		{
			RunId = runId,
			StartedAtUtc = startedAt,
			CompletedAtUtc = DateTimeOffset.UtcNow,
			DueCount = dueRecords.Count,
			AttemptedCount = attemptedCount,
			SucceededCount = succeededCount,
			FailedCount = failedCount,
			SkippedCount = skippedCount,
			Failures = failures
		};

		_logger.LogInformation(
			"Scheduled processing run {RunId} completed: Due={DueCount}, Attempted={AttemptedCount}, Succeeded={SucceededCount}, Failed={FailedCount}, Skipped={SkippedCount}",
			summary.RunId,
			summary.DueCount,
			summary.AttemptedCount,
			summary.SucceededCount,
			summary.FailedCount,
			summary.SkippedCount);

		return summary;
	}

	private async Task ProcessClaimAsync(ScheduledSocialPostRecord record, CancellationToken cancellationToken)
	{
		var publishingStarted = false;
		try
		{
			// Freeze defaults for legacy pending jobs before making any external changes.
			if (record.TargetPlatforms.Count == 0)
			{
				record.TargetPlatforms = _socialPostService.GetTargetPlatformNames([]);
			}

			var remaining = record.TargetPlatforms
				.Where(platform => !record.DeliveryResults.Any(result =>
					result.Success && string.Equals(result.Platform, platform, StringComparison.OrdinalIgnoreCase)))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			var post = remaining.Count > 0 ? await MapToSocialPostAsync(record, cancellationToken) : null;
			foreach (var platform in remaining)
			{
				cancellationToken.ThrowIfCancellationRequested();
				record.LeaseExpiresAtUtc = DateTimeOffset.UtcNow + ScheduledPostClaimPolicy.LeaseDuration;
				await _scheduledSocialPostRepository.SaveClaimAsync(record, cancellationToken);
				publishingStarted = true;

				var result = await PublishPlatformAsync(post!, platform, cancellationToken);
				record.DeliveryResults.RemoveAll(r => string.Equals(r.Platform, platform, StringComparison.OrdinalIgnoreCase));
				record.DeliveryResults.Add(WithoutExceptions(result));
				await _scheduledSocialPostRepository.SaveClaimAsync(record, cancellationToken);
			}

			SetDeliveryOutcome(record);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process scheduled post {ScheduledPostId}", record.ScheduledPostId);
			record.Status = publishingStarted ? ScheduledPostStatus.NeedsReview : ScheduledPostStatus.Failed;
			record.LastErrorCode = publishingStarted ? "DELIVERY_UNCERTAIN" : "PREPARATION_FAILED";
			record.LastErrorMessage = publishingStarted
				? "Delivery may have succeeded. Check provider posts before retrying."
				: "Unable to prepare scheduled post images or delivery state.";
		}

		record.LeaseExpiresAtUtc = null;
		await _scheduledSocialPostRepository.SaveClaimAsync(record, cancellationToken);
	}

	private async Task<PlatformPostResult> PublishPlatformAsync(
		SocialPost post, string platform, CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromMinutes(5));
		var results = await _socialPostService.PostAsync(new SocialPost
		{
			Text = post.Text,
			Hashtags = post.Hashtags,
			Images = post.Images,
			ImageUrls = post.ImageUrls,
			AutoThread = post.AutoThread,
			TargetPlatforms = [platform]
		}, timeout.Token);
		return results.SingleOrDefault() ?? new PlatformPostResult
		{
			Platform = platform,
			Success = false,
			ErrorCode = "PLATFORM_NOT_CONFIGURED",
			ErrorMessage = "The scheduled target platform is not configured."
		};
	}

	private static void SetDeliveryOutcome(ScheduledSocialPostRecord record)
	{
		var allSucceeded = record.TargetPlatforms.Count > 0 && record.TargetPlatforms.All(platform =>
			record.DeliveryResults.Any(result => result.Success
				&& string.Equals(result.Platform, platform, StringComparison.OrdinalIgnoreCase)));
		record.Status = allSucceeded ? ScheduledPostStatus.Published
			: record.DeliveryResults.Any(NeedsReview) ? ScheduledPostStatus.NeedsReview : ScheduledPostStatus.Failed;
		record.PublishedAtUtc = allSucceeded ? DateTimeOffset.UtcNow : null;
		record.LastErrorCode = allSucceeded ? null
			: record.Status == ScheduledPostStatus.NeedsReview ? "DELIVERY_UNCERTAIN" : "PARTIAL_PLATFORM_FAILURE";
		record.LastErrorMessage = allSucceeded ? null
			: record.Status == ScheduledPostStatus.NeedsReview
				? "Delivery may have partially succeeded. Check provider posts before retrying."
				: "Only unsuccessful platforms will be retried on the next processing run.";
	}

	private static bool NeedsReview(PlatformPostResult result)
	{
		if (result.Success)
		{
			return false;
		}

		if (result.ThreadResults is { Count: > 0 } segments)
		{
			return segments.Any(segment => segment.Success)
				|| segments.Any(segment => segment.ErrorCode != "THREAD_BROKEN" && NeedsReview(segment));
		}

		return result.ErrorCode is not ("AUTH_FAILED" or "RATE_LIMITED" or "VALIDATION_FAILED"
			or "IMAGE_DOWNLOAD_FAILED" or "IMAGE_UPLOAD_FAILED" or "PLATFORM_NOT_CONFIGURED");
	}

	private static PlatformPostResult WithoutExceptions(PlatformPostResult result)
	{
		return result with
		{
			Error = null,
			PublishedText = null,
			ThreadResults = result.ThreadResults?.Select(WithoutExceptions).ToList()
		};
	}

	private async Task NotifyScheduledPostFailureAsync(
		ScheduledSocialPostRecord record,
		IReadOnlyList<PlatformPostResult> platformResults,
		string errorCode,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		if (_emailNotificationService is null)
		{
			return;
		}

		var failures = platformResults.Where(r => !r.Success).ToList();
		var errorDetails = failures.Count > 0
			? string.Join("\n\n", failures.Select(f =>
				$"Platform: {f.Platform}\nError Code: {f.ErrorCode ?? "N/A"}\nError Message: {f.ErrorMessage ?? "N/A"}"))
			: $"Error Code: {errorCode}\nError Message: {errorMessage}";

		var context = new Dictionary<string, string>
		{
			["Scheduled Post ID"] = record.ScheduledPostId,
			["Scheduled For"] = record.ScheduledForUtc.ToString("yyyy-MM-dd HH:mm:ss"),
			["Post Text"] = record.Text.Length > 100 ? record.Text[..100] + "..." : record.Text,
			["Target Platforms"] = string.Join(", ", record.TargetPlatforms),
			["Attempt Count"] = record.AttemptCount.ToString(),
			["Total Platforms"] = platformResults.Count.ToString(),
			["Failed Platforms"] = failures.Count.ToString(),
			["Successful Platforms"] = platformResults.Count(r => r.Success).ToString()
		};

		await _emailNotificationService.SendPostFailureNotificationAsync(
			"Scheduled Social Post",
			errorDetails,
			context,
			cancellationToken);

		_logger.LogInformation(
			"Sent failure notification email for scheduled post {ScheduledPostId}",
			record.ScheduledPostId);
	}

	private async Task<SocialPost> MapToSocialPostAsync(ScheduledSocialPostRecord record, CancellationToken cancellationToken)
	{
		var images = new List<ImageData>();
		foreach (var storedImage in record.UploadedImages)
		{
			var content = await _scheduledPostImageStore.DownloadAsync(storedImage.BlobName, cancellationToken);
			images.Add(new ImageData
			{
				Content = content,
				ContentType = storedImage.ContentType,
				AltText = storedImage.AltText,
				FileName = storedImage.FileName
			});
		}

		return new SocialPost
		{
			Text = record.Text,
			AutoThread = record.AutoThread,
			Hashtags = record.Hashtags,
			TargetPlatforms = record.TargetPlatforms,
			ImageUrls = record.ImageUrls,
			Images = images
		};
	}
}
