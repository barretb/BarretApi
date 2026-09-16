using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class UpdateScheduledPostEndpoint(ScheduledPostManagementService management)
	: Endpoint<UpdateScheduledPostRequest, ScheduledPostDetailsResponse>
{
	private readonly ScheduledPostManagementService _management = management;

	public override void Configure()
	{
		Patch("/api/social-posts/scheduled/{Id}");
		Summary(s =>
		{
			s.Summary = "Edit or reschedule an unattempted pending post";
			s.Description = "Requires the exact version from the latest details response. No provider API is called.";
			s.Responses[200] = "Updated post details and version.";
			s.Responses[400] = "Invalid request.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "Scheduled post not found.";
			s.Responses[409] = "Stale version or operation not permitted in the current state.";
		});
	}

	public override Task HandleAsync(UpdateScheduledPostRequest req, CancellationToken ct)
	{
		return ScheduledPostEndpointResponses.ExecuteAsync(HttpContext,
			() => _management.UpdateAsync(Route<string>("Id")!,
				new UpdateScheduledPostCommand(req.Version, req.Text, req.Hashtags, req.Platforms,
				req.Images?.Select(i => new ImageUrl { Url = i.Url, AltText = i.AltText }).ToList(),
				req.AutoThread, req.ScheduledFor, req.RemoveUploadedImages), ct), ct);
	}
}
