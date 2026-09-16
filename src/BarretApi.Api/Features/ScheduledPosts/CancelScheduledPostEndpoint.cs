using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class CancelScheduledPostEndpoint(ScheduledPostManagementService management)
	: Endpoint<CancelScheduledPostRequest, ScheduledPostDetailsResponse>
{
	private readonly ScheduledPostManagementService _management = management;

	public override void Configure()
	{
		Post("/api/social-posts/scheduled/{Id}/cancel");
		Summary(s =>
		{
			s.Summary = "Cancel a pending post without deleting its history";
			s.Description = "Requires the exact version from the latest details response. No provider API is called.";
			s.Responses[200] = "Updated post details and version.";
			s.Responses[400] = "Invalid request.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "Scheduled post not found.";
			s.Responses[409] = "Stale version or operation not permitted in the current state.";
		});
	}

	public override Task HandleAsync(CancelScheduledPostRequest req, CancellationToken ct)
	{
		return ScheduledPostEndpointResponses.ExecuteAsync(HttpContext,
			() => _management.CancelAsync(Route<string>("Id")!,
				new CancelScheduledPostCommand(req.Version, req.Note), ct), ct);
	}
}
