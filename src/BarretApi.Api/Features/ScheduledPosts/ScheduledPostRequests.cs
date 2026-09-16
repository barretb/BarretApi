using FastEndpoints;

namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class ListScheduledPostsRequest
{
	[QueryParam] public string? Status { get; set; }
	[QueryParam] public DateTimeOffset? From { get; set; }
	[QueryParam] public DateTimeOffset? To { get; set; }
	[QueryParam] public int PageSize { get; set; } = 50;
	[QueryParam] public string? ContinuationToken { get; set; }
}

public sealed class GetScheduledPostRequest
{
	public string Id { get; set; } = string.Empty;
}

public sealed class UpdateScheduledPostRequest
{
	public string Id { get; set; } = string.Empty;
	public string Version { get; set; } = string.Empty;
	public string? Text { get; set; }
	public List<string>? Hashtags { get; set; }
	public List<string>? Platforms { get; set; }
	public List<ScheduledPostImageRequest>? Images { get; set; }
	public bool? AutoThread { get; set; }
	public DateTimeOffset? ScheduledFor { get; set; }
	public bool RemoveUploadedImages { get; set; }
}

public sealed class ScheduledPostImageRequest
{
	public string Url { get; set; } = string.Empty;
	public string AltText { get; set; } = string.Empty;
}

public sealed class CancelScheduledPostRequest
{
	public string Id { get; set; } = string.Empty;
	public string Version { get; set; } = string.Empty;
	public string Note { get; set; } = string.Empty;
}

public sealed class ReconcileScheduledPostRequest
{
	public string Id { get; set; } = string.Empty;
	public string Version { get; set; } = string.Empty;
	public string Note { get; set; } = string.Empty;
	public List<PublishedDeliveryRequest> PublishedDeliveries { get; set; } = [];
}

public sealed class PublishedDeliveryRequest
{
	public string Platform { get; set; } = string.Empty;
	public string PostId { get; set; } = string.Empty;
	public string? PostUrl { get; set; }
}

public sealed class RetryScheduledPostRequest
{
	public string Id { get; set; } = string.Empty;
	public string Version { get; set; } = string.Empty;
	public string Note { get; set; } = string.Empty;
	public List<string> Platforms { get; set; } = [];
	public bool ConfirmNotPublished { get; set; }
	public DateTimeOffset? ScheduledFor { get; set; }
}
