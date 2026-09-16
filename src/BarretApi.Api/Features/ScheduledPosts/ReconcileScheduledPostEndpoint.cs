using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class ReconcileScheduledPostEndpoint(ScheduledPostManagementService management)
	: Endpoint<ReconcileScheduledPostRequest, ScheduledPostDetailsResponse>
{
	private readonly ScheduledPostManagementService _management = management;

	public override void Configure()
	{
		Post("/api/social-posts/scheduled/{Id}/reconcile");
		Summary(s =>
		{
			s.Summary = "Confirm deliveries verified on the provider; does not publish or retry";
			s.Description = "Requires the exact version from the latest details response. No provider API is called.";
			s.Responses[200] = "Updated post details and version.";
			s.Responses[400] = "Invalid request.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "Scheduled post not found.";
			s.Responses[409] = "Stale version or operation not permitted in the current state.";
		});
	}

	public override Task HandleAsync(ReconcileScheduledPostRequest req, CancellationToken ct)
	{
		return ScheduledPostEndpointResponses.ExecuteAsync(HttpContext,
			() => _management.ReconcileAsync(Route<string>("Id")!,
				new ReconcileScheduledPostCommand(req.Version,
				req.PublishedDeliveries.Select(d => new ConfirmedPostDelivery(d.Platform, d.PostId, d.PostUrl)).ToList(), req.Note), ct), ct);
	}
}
