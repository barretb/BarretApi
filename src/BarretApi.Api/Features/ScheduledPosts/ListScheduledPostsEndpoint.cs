using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class ListScheduledPostsEndpoint(IScheduledSocialPostRepository repository)
	: Endpoint<ListScheduledPostsRequest, ListScheduledPostsResponse>
{
	private readonly IScheduledSocialPostRepository _repository = repository;

	public override void Configure()
	{
		Get("/api/social-posts/scheduled");
		Summary(s =>
		{
			s.Summary = "List scheduled posts and their publishing history";
			s.Description = "Filters by status and inclusive scheduled UTC date range. Results use storage-key order and opaque continuation tokens.";
			s.Responses[200] = "One page of scheduled posts.";
			s.Responses[400] = "Invalid filter or continuation token.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
		});
	}

	public override async Task HandleAsync(ListScheduledPostsRequest req, CancellationToken ct)
	{
		var status = req.Status is null ? (ScheduledPostStatus?)null : Enum.Parse<ScheduledPostStatus>(req.Status, true);
		try
		{
			var page = await _repository.ListAsync(new ScheduledPostsQuery(status, req.From, req.To, req.PageSize, req.ContinuationToken), ct);
			await Send.OkAsync(new ListScheduledPostsResponse
			{
				Posts = page.Posts.Select(ScheduledPostResponseMapper.ToSummary).ToList(),
				ContinuationToken = page.ContinuationToken
			}, ct);
		}
		catch (ArgumentException ex)
		{
			AddError(ex.Message);
			await Send.ErrorsAsync(400, ct);
		}
	}
}
