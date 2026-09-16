using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class GetScheduledPostEndpoint(IScheduledSocialPostRepository repository)
	: Endpoint<GetScheduledPostRequest, ScheduledPostDetailsResponse>
{
	private readonly IScheduledSocialPostRepository _repository = repository;

	public override void Configure()
	{
		Get("/api/social-posts/scheduled/{Id}");
		Summary(s =>
		{
			s.Summary = "Inspect a scheduled post and platform delivery receipts";
			s.Responses[200] = "Post details, version, delivery receipts, and operator confirmations.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "Scheduled post not found.";
		});
	}

	public override async Task HandleAsync(GetScheduledPostRequest req, CancellationToken ct)
	{
		var record = await _repository.GetByIdAsync(Route<string>("Id")!, ct);
		if (record is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		await Send.OkAsync(ScheduledPostResponseMapper.ToDetails(record), ct);
	}
}
