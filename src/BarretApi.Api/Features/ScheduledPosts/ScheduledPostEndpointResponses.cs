using BarretApi.Core.Models;

namespace BarretApi.Api.Features.ScheduledPosts;

internal static class ScheduledPostEndpointResponses
{
	public static async Task ExecuteAsync(HttpContext context, Func<Task<PostManagementResult>> action, CancellationToken ct)
	{
		PostManagementResult result;
		try
		{
			result = await action();
		}
		catch (ArgumentException ex)
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			await context.Response.WriteAsJsonAsync(new { message = ex.Message }, ct);
			return;
		}

		context.Response.StatusCode = result.Outcome switch
		{
			PostManagementOutcome.Success => StatusCodes.Status200OK,
			PostManagementOutcome.NotFound => StatusCodes.Status404NotFound,
			_ => StatusCodes.Status409Conflict
		};
		if (result.Outcome == PostManagementOutcome.Success)
		{
			await context.Response.WriteAsJsonAsync(ScheduledPostResponseMapper.ToDetails(result.Post!), ct);
		}
		else
		{
			await context.Response.WriteAsJsonAsync(new { message = result.Message ?? "Scheduled post not found." }, ct);
		}
	}
}
