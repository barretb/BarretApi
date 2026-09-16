using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class RetryScheduledPostEndpoint(ScheduledPostManagementService management)
	: Endpoint<RetryScheduledPostRequest, ScheduledPostDetailsResponse>
{
	private readonly ScheduledPostManagementService _management = management;

	public override void Configure()
	{
		Post("/api/social-posts/scheduled/{Id}/retry");
		Summary(s =>
		{
			s.Summary = "Queue an explicit retry of all verified remaining non-deliveries";
			s.Description = "Requires the exact version from the latest details response. No provider API is called.";
			s.Responses[200] = "Updated post details and version.";
			s.Responses[400] = "Invalid request.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "Scheduled post not found.";
			s.Responses[409] = "Stale version or operation not permitted in the current state.";
		});
	}

	public override Task HandleAsync(RetryScheduledPostRequest req, CancellationToken ct)
	{
		return ScheduledPostEndpointResponses.ExecuteAsync(HttpContext,
			() => _management.RetryAsync(Route<string>("Id")!,
				new RetryScheduledPostCommand(req.Version, req.Platforms, req.ConfirmNotPublished, req.Note, req.ScheduledFor), ct), ct);
	}
}
