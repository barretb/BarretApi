using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.SocialPost;

public sealed class PostTipOfDayEndpoint(
	TipOfDayService tipOfDayService,
	ILogger<PostTipOfDayEndpoint> logger)
	: Endpoint<PostTipOfDayRequest, PostTipOfDayResponse>
{
	public override void Configure()
	{
		Post("/api/social-posts/tips/post");

		Summary(s =>
		{
			s.Summary = "Post a tip of the day";
			s.Description = "Selects a random eligible tip by category, posts it to selected social platforms, and marks it posted after a successful platform post.";
			s.ExampleRequest = new PostTipOfDayRequest
			{
				Category = "dotnet",
				Platforms = ["bluesky", "mastodon"],
				Leader = "Tip of the day"
			};
			s.Responses[200] = "All targeted platforms succeeded.";
			s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[422] = "No eligible tips found for the requested category.";
			s.Responses[502] = "All targeted platforms failed to post.";
		});
	}

	public override async Task HandleAsync(PostTipOfDayRequest req, CancellationToken ct)
	{
		logger.LogInformation(
			"Tip of the day post request received for category {Category}",
			req.Category);

		TipOfDayPostResult result;
		try
		{
			result = await tipOfDayService.SelectAndPostAsync(
				new TipOfDayPostCommand
				{
					Category = req.Category!,
					Platforms = req.Platforms ?? [],
					Leader = req.Leader
				},
				ct);
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("No eligible tips", StringComparison.OrdinalIgnoreCase))
		{
			logger.LogWarning(
				"No eligible tip of the day found for category {Category}: {Error}",
				req.Category,
				ex.Message);
			AddError(ex.Message);
			await Send.ErrorsAsync(422, ct);
			return;
		}

		var response = BuildResponse(result);
		var statusCode = DetermineStatusCode(result.PlatformResults);

		logger.LogInformation(
			"Tip of the day post completed with status {StatusCode} for category {Category}",
			statusCode,
			result.SelectedTip.Category);

		await Send.ResponseAsync(response, statusCode, ct);
	}

	private static PostTipOfDayResponse BuildResponse(TipOfDayPostResult result)
	{
		return new PostTipOfDayResponse
		{
			TipId = result.SelectedTip.TipId,
			Category = result.SelectedTip.Category,
			Tip = result.SelectedTip.Tip,
			MoreInfoUrl = result.SelectedTip.MoreInfoUrl,
			PreviousLastPostedDate = result.SelectedTip.LastPostedDate,
			TipMarkedPosted = result.TipMarkedPosted,
			Results = result.PlatformResults.Select(r => new PlatformResult
			{
				Platform = r.Platform,
				Success = r.Success,
				PostId = r.PostId,
				PostUrl = r.PostUrl,
				ShortenedText = r.PublishedText,
				Error = r.Success ? null : r.ErrorMessage,
				ErrorCode = r.Success ? null : r.ErrorCode
			}).ToList(),
			PostedAt = result.AttemptedAtUtc
		};
	}

	private static int DetermineStatusCode(IReadOnlyList<PlatformPostResult> results)
	{
		if (results.All(r => r.Success))
		{
			return 200;
		}

		if (results.Any(r => r.Success))
		{
			return 207;
		}

		return 502;
	}
}
