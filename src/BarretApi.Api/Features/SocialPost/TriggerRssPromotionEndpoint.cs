using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.SocialPost;

public sealed class TriggerRssPromotionEndpoint(
	IBlogPromotionOrchestrator blogPromotionOrchestrator,
	ILogger<TriggerRssPromotionEndpoint> logger)
	: EndpointWithoutRequest<TriggerRssPromotionResponse>
{
	private readonly IBlogPromotionOrchestrator _blogPromotionOrchestrator = blogPromotionOrchestrator;
	private readonly ILogger<TriggerRssPromotionEndpoint> _logger = logger;

	public override void Configure()
	{
		Post("/api/social-posts/rss-promotion");

		Summary(s =>
		{
			s.Summary = "Trigger RSS blog promotion";
			s.Description = "Checks RSS feed, posts new blog entries first, then eligible reminder posts.";
			s.Responses[200] = "Promotion run completed. Includes counts and failure details.";
			s.Responses[400] = "Invalid blog-promotion configuration.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[502] = "Feed read failed or all posting attempts failed.";
		});
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		try
		{
			var summary = await _blogPromotionOrchestrator.RunAsync(ct);
			var response = MapToResponse(summary);
			var statusCode = DetermineStatusCode(summary);

			await Send.ResponseAsync(response, statusCode, ct);
		}
		catch (InvalidOperationException ex)
		{
			_logger.LogWarning(ex, "Invalid RSS promotion configuration");
			AddError(ex.Message);
			await Send.ErrorsAsync(cancellation: ct);
		}
	}

	private static TriggerRssPromotionResponse MapToResponse(PromotionRunSummary summary)
	{
		return new TriggerRssPromotionResponse
		{
			RunId = summary.RunId,
			StartedAtUtc = summary.StartedAtUtc,
			CompletedAtUtc = summary.CompletedAtUtc,
			EntriesEvaluated = summary.EntriesEvaluated,
			NewPostsAttempted = summary.NewPostsAttempted,
			NewPostsSucceeded = summary.NewPostsSucceeded,
			ReminderPostsAttempted = summary.ReminderPostsAttempted,
			ReminderPostsSucceeded = summary.ReminderPostsSucceeded,
			EntriesSkippedAlreadyPosted = summary.EntriesSkippedAlreadyPosted,
			EntriesSkippedOutsideWindow = summary.EntriesSkippedOutsideWindow,
			LastTwoBlogPosts = summary.LastTwoBlogPosts.Select(p => new TriggerRssPromotionBlogPost
			{
				EntryIdentity = p.EntryIdentity,
				CanonicalUrl = p.CanonicalUrl,
				Title = p.Title,
				PublishedAtUtc = p.PublishedAtUtc
			}).ToList(),
			Failures = summary.Failures.Select(f => new TriggerRssPromotionFailure
			{
				EntryIdentity = f.EntryIdentity,
				CanonicalUrl = f.CanonicalUrl,
				Phase = f.Phase.ToString(),
				Platform = f.Platform,
				ErrorCode = f.ErrorCode,
				ErrorMessage = f.ErrorMessage
			}).ToList()
		};
	}

	private static int DetermineStatusCode(PromotionRunSummary summary)
	{
		if (summary.Failures.Any(f => f.ErrorCode == "RSS_FEED_READ_FAILED"))
		{
			return 502;
		}

		var attempted = summary.NewPostsAttempted + summary.ReminderPostsAttempted;
		var succeeded = summary.NewPostsSucceeded + summary.ReminderPostsSucceeded;
		if (attempted > 0 && succeeded == 0)
		{
			return 502;
		}

		return 200;
	}
}
