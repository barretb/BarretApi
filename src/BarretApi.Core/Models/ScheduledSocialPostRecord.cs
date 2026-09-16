namespace BarretApi.Core.Models;

/// <summary>
/// Durable representation of a post scheduled for future publishing.
/// </summary>
public sealed class ScheduledSocialPostRecord
{
	public required string ScheduledPostId { get; init; }
	public required DateTimeOffset ScheduledForUtc { get; set; }
	public required ScheduledPostStatus Status { get; set; }
	public required string Text { get; set; }
	public IReadOnlyList<string> Hashtags { get; set; } = [];
	public IReadOnlyList<string> TargetPlatforms { get; set; } = [];
	public IReadOnlyList<ImageUrl> ImageUrls { get; set; } = [];
	public IReadOnlyList<StoredImageData> UploadedImages { get; set; } = [];
	public required DateTimeOffset CreatedAtUtc { get; init; }
	public DateTimeOffset? LastAttemptedAtUtc { get; set; }
	public DateTimeOffset? PublishedAtUtc { get; set; }
	public string? LastErrorCode { get; set; }
	public string? LastErrorMessage { get; set; }
	public bool AutoThread { get; set; }
	public bool DeliveryTrackingEnabled { get; set; }
	public List<PlatformPostResult> DeliveryResults { get; set; } = [];
	public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
	public string? Version { get; set; }
	public DateTimeOffset? UpdatedAtUtc { get; set; }
	public string? LastManagementAction { get; set; }
	public string? ManagementNote { get; set; }
	public List<PostDeliveryConfirmation> DeliveryConfirmations { get; set; } = [];
	public int AttemptCount { get; set; }
}

public sealed class StoredImageData
{
	public required string BlobName { get; init; }
	public required string ContentType { get; init; }
	public required string AltText { get; init; }
	public string? FileName { get; init; }
}
