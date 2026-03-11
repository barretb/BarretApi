using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

public sealed class BlogPromotionOrchestrator(
	IBlogFeedReader blogFeedReader,
	IBlogPostPromotionRepository promotionRepository,
	SocialPostService socialPostService,
	IOptions<BlogPromotionOptions> blogPromotionOptions,
	ILogger<BlogPromotionOrchestrator> logger) : IBlogPromotionOrchestrator
{
	private readonly IBlogFeedReader _blogFeedReader = blogFeedReader;
	private readonly IBlogPostPromotionRepository _promotionRepository = promotionRepository;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly BlogPromotionOptions _options = blogPromotionOptions.Value;
	private readonly ILogger<BlogPromotionOrchestrator> _logger = logger;

	public async Task<PromotionRunSummary> RunAsync(CancellationToken cancellationToken = default)
	{
		ValidateOptions();

		var now = DateTimeOffset.UtcNow;
		var runId = now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
		var summary = new PromotionRunSummary
		{
			RunId = runId,
			StartedAtUtc = now,
			CompletedAtUtc = now
		};

		using var scope = _logger.BeginScope(new Dictionary<string, object>
		{
			["PromotionRunId"] = runId
		});

		List<BlogFeedEntry> feedEntries;
		try
		{
			feedEntries = (await _blogFeedReader.ReadEntriesAsync(cancellationToken)).ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to read RSS feed");
			summary.Failures.Add(new PromotionEntryFailure
			{
				EntryIdentity = "rss-feed",
				CanonicalUrl = _options.FeedUrl,
				Phase = PromotionPhase.Initial,
				Platform = "rss-feed",
				ErrorCode = "RSS_FEED_READ_FAILED",
				ErrorMessage = ex.Message
			});
			summary.CompletedAtUtc = DateTimeOffset.UtcNow;
			return summary;
		}

		summary.EntriesEvaluated = feedEntries.Count;
		var feedEntriesByIdentity = feedEntries.ToDictionary(e => e.EntryIdentity, StringComparer.Ordinal);
		summary.LastTwoBlogPosts.AddRange(
			feedEntries
				.OrderByDescending(e => e.PublishedAtUtc)
				.Take(2)
				.Select(e => new RecentBlogPostDetails
				{
					EntryIdentity = e.EntryIdentity,
					CanonicalUrl = e.CanonicalUrl,
					Title = e.Title,
					PublishedAtUtc = e.PublishedAtUtc
				}));

		var cutoff = now.AddDays(-_options.RecentDaysWindow);

		_logger.LogInformation(
			"Starting initial-post phase for {EntryCount} entries with cutoff {CutoffUtc}",
			feedEntries.Count,
			cutoff);

		foreach (var entry in feedEntries.OrderByDescending(e => e.PublishedAtUtc))
		{
			if (entry.Tags.Count == 0)
			{
				summary.EntriesSkippedNoTags++;
				continue;
			}

			if (entry.PublishedAtUtc < cutoff)
			{
				summary.EntriesSkippedOutsideWindow++;
				continue;
			}

			var existing = await _promotionRepository.GetByEntryIdentityAsync(entry.EntryIdentity, cancellationToken);
			if (existing is not null && existing.InitialPostStatus == PostAttemptStatus.Succeeded)
			{
				summary.EntriesSkippedAlreadyPosted++;
				continue;
			}

			summary.NewPostsAttempted++;
			var initialRecord = existing ?? new BlogPostPromotionRecord
			{
				EntryIdentity = entry.EntryIdentity,
				CanonicalUrl = entry.CanonicalUrl,
				Title = entry.Title,
				PublishedAtUtc = entry.PublishedAtUtc
			};

			initialRecord.CanonicalUrl = entry.CanonicalUrl;
			initialRecord.Title = entry.Title;
			initialRecord.PublishedAtUtc = entry.PublishedAtUtc;
			initialRecord.InitialPostAttemptedAtUtc = now;
			initialRecord.LastProcessedAtUtc = now;

			var initialResults = await _socialPostService.PostAsync(
				new SocialPost
				{
					Text = BuildInitialPostText(entry),
					Hashtags = entry.Tags,
					ImageUrls = BuildImageUrls(entry),
					TargetPlatforms = []
				},
				cancellationToken);

			var initialSucceeded = initialResults.Any(r => r.Success);
			initialRecord.InitialPostStatus = initialSucceeded ? PostAttemptStatus.Succeeded : PostAttemptStatus.Failed;
			initialRecord.InitialPostSucceededAtUtc = initialSucceeded ? now : null;
			initialRecord.InitialPostResultCode = initialSucceeded ? "OK" : "INITIAL_POST_FAILED";

			if (initialSucceeded)
			{
				summary.NewPostsSucceeded++;
			}

			CaptureFailures(summary, entry, PromotionPhase.Initial, initialResults);
			await _promotionRepository.UpsertAsync(initialRecord, cancellationToken);
		}

		if (_options.EnableReminderPosts)
		{
			_logger.LogInformation("Starting reminder-post phase");
			var reminderCutoff = now.AddHours(-_options.ReminderDelayHours);
			var trackedRecords = await _promotionRepository.GetAllAsync(cancellationToken);

			foreach (var trackedRecord in trackedRecords)
			{
				if (trackedRecord.InitialPostStatus != PostAttemptStatus.Succeeded)
				{
					continue;
				}

				if (trackedRecord.ReminderPostStatus == PostAttemptStatus.Succeeded)
				{
					continue;
				}

				if (trackedRecord.InitialPostSucceededAtUtc is null)
				{
					continue;
				}

				if (trackedRecord.InitialPostSucceededAtUtc.Value >= summary.StartedAtUtc)
				{
					continue;
				}

				if (trackedRecord.InitialPostSucceededAtUtc.Value > reminderCutoff)
				{
					continue;
				}

				summary.ReminderPostsAttempted++;
				trackedRecord.ReminderPostAttemptedAtUtc = now;
				trackedRecord.LastProcessedAtUtc = now;

				var reminderResults = await _socialPostService.PostAsync(
					new SocialPost
					{
						Text = BuildReminderPostText(trackedRecord),
						Hashtags = ResolveReminderTags(feedEntriesByIdentity, trackedRecord),
						ImageUrls = ResolveReminderImageUrls(feedEntriesByIdentity, trackedRecord),
						TargetPlatforms = []
					},
					cancellationToken);

				var reminderSucceeded = reminderResults.Any(r => r.Success);
				trackedRecord.ReminderPostStatus = reminderSucceeded ? PostAttemptStatus.Succeeded : PostAttemptStatus.Failed;
				trackedRecord.ReminderPostSucceededAtUtc = reminderSucceeded ? now : null;
				trackedRecord.ReminderPostResultCode = reminderSucceeded ? "OK" : "REMINDER_POST_FAILED";

				if (reminderSucceeded)
				{
					summary.ReminderPostsSucceeded++;
				}

				CaptureFailures(summary, trackedRecord, PromotionPhase.Reminder, reminderResults);
				await _promotionRepository.UpsertAsync(trackedRecord, cancellationToken);
			}
		}

		summary.CompletedAtUtc = DateTimeOffset.UtcNow;
		return summary;
	}

	private void ValidateOptions()
	{
		_options.ThrowIfInvalid();
	}

	private static string BuildInitialPostText(BlogFeedEntry entry)
		=> $"{entry.Title}\n{entry.CanonicalUrl}";

	private static string BuildReminderPostText(BlogPostPromotionRecord record)
		=> $"In case you missed it earlier...\n\n{record.Title}\n{record.CanonicalUrl}";

	private static IReadOnlyList<ImageUrl> BuildImageUrls(BlogFeedEntry entry)
	{
		if (!Uri.TryCreate(entry.HeroImageUrl, UriKind.Absolute, out var heroUri))
		{
			return [];
		}

		if (heroUri.Scheme is not ("http" or "https"))
		{
			return [];
		}

		return
		[
			new ImageUrl
			{
				Url = heroUri.ToString(),
				AltText = "blog post hero image"
			}
		];
	}

	private static IReadOnlyList<string> ResolveReminderTags(
		IReadOnlyDictionary<string, BlogFeedEntry> feedEntriesByIdentity,
		BlogPostPromotionRecord trackedRecord)
	{
		if (feedEntriesByIdentity.TryGetValue(trackedRecord.EntryIdentity, out var entry))
		{
			return entry.Tags;
		}

		return [];
	}

	private static IReadOnlyList<ImageUrl> ResolveReminderImageUrls(
		IReadOnlyDictionary<string, BlogFeedEntry> feedEntriesByIdentity,
		BlogPostPromotionRecord trackedRecord)
	{
		if (feedEntriesByIdentity.TryGetValue(trackedRecord.EntryIdentity, out var entry))
		{
			return BuildImageUrls(entry);
		}

		return [];
	}

	private static void CaptureFailures(
		PromotionRunSummary summary,
		BlogFeedEntry entry,
		PromotionPhase phase,
		IReadOnlyList<PlatformPostResult> results)
	{
		foreach (var failure in results.Where(r => !r.Success))
		{
			summary.Failures.Add(new PromotionEntryFailure
			{
				EntryIdentity = entry.EntryIdentity,
				CanonicalUrl = entry.CanonicalUrl,
				Phase = phase,
				Platform = failure.Platform,
				ErrorCode = failure.ErrorCode ?? "PLATFORM_ERROR",
				ErrorMessage = failure.ErrorMessage ?? "Platform post failed"
			});
		}
	}

	private static void CaptureFailures(
		PromotionRunSummary summary,
		BlogPostPromotionRecord record,
		PromotionPhase phase,
		IReadOnlyList<PlatformPostResult> results)
	{
		foreach (var failure in results.Where(r => !r.Success))
		{
			summary.Failures.Add(new PromotionEntryFailure
			{
				EntryIdentity = record.EntryIdentity,
				CanonicalUrl = record.CanonicalUrl,
				Phase = phase,
				Platform = failure.Platform,
				ErrorCode = failure.ErrorCode ?? "PLATFORM_ERROR",
				ErrorMessage = failure.ErrorMessage ?? "Platform post failed"
			});
		}
	}
}
