using BarretApi.Core.Models;

namespace BarretApi.Api.Features.ScheduledPosts;

internal static class ScheduledPostResponseMapper
{
	public static ScheduledPostSummary ToSummary(ScheduledSocialPostRecord record)
	{
		var elements = new System.Globalization.StringInfo(record.Text);
		return new ScheduledPostSummary
		{
			ScheduledPostId = record.ScheduledPostId,
			Status = record.Status.ToString(),
			Version = record.Version ?? string.Empty,
			TextPreview = elements.LengthInTextElements > 160 ? elements.SubstringByTextElements(0, 160) + "…" : record.Text,
			ScheduledForUtc = record.ScheduledForUtc,
			CreatedAtUtc = record.CreatedAtUtc,
			PublishedAtUtc = record.PublishedAtUtc,
			Platforms = record.TargetPlatforms,
			AttemptCount = record.AttemptCount,
			SuccessfulPlatformCount = record.DeliveryResults.Count(r => r.Success)
		};
	}

	public static ScheduledPostDetailsResponse ToDetails(ScheduledSocialPostRecord record)
	{
		return new ScheduledPostDetailsResponse
		{
			Post = ToSummary(record),
			Text = record.Text,
			Hashtags = record.Hashtags,
			AutoThread = record.AutoThread,
			Images = record.ImageUrls.Select(i => new ScheduledPostImageDetails("url", i.Url, i.AltText, null, null))
				.Concat(record.UploadedImages.Select(i => new ScheduledPostImageDetails("upload", null, i.AltText, i.ContentType, i.FileName))).ToList(),
			Deliveries = record.DeliveryResults.Select(ToDelivery).ToList(),
			Confirmations = record.DeliveryConfirmations.Select(c => new DeliveryConfirmationDetails(c.Platform, c.PostId, c.PostUrl, c.ConfirmedAtUtc, c.Note)).ToList(),
			LastAttemptedAtUtc = record.LastAttemptedAtUtc,
			LeaseExpiresAtUtc = record.LeaseExpiresAtUtc,
			UpdatedAtUtc = record.UpdatedAtUtc,
			LastErrorCode = record.LastErrorCode,
			LastErrorMessage = record.LastErrorMessage,
			LastManagementAction = record.LastManagementAction,
			ManagementNote = record.ManagementNote
		};
	}

	private static ScheduledPostDeliveryDetails ToDelivery(PlatformPostResult result)
	{
		return new ScheduledPostDeliveryDetails
		{
			Platform = result.Platform,
			Success = result.Success,
			PostId = result.PostId,
			PostUrl = result.PostUrl,
			ErrorCode = result.ErrorCode,
			ErrorMessage = result.ErrorMessage,
			ThreadResults = result.ThreadResults?.Select(ToDelivery).ToList()
		};
	}
}
