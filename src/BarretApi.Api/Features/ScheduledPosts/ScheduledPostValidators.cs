using System.Linq.Expressions;
using BarretApi.Core.Models;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.ScheduledPosts;

internal static class ScheduledPostValidation
{
	public static void RequireIdentity<T>(AbstractValidator<T> validator, Expression<Func<T, string>> id)
	{
		validator.RuleFor(id).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9_-]+$");
	}

	public static void RequireVersion<T>(AbstractValidator<T> validator, Expression<Func<T, string>> id, Expression<Func<T, string>> version)
	{
		RequireIdentity(validator, id);
		validator.RuleFor(version).NotEmpty().MaximumLength(256).NotEqual("*");
	}

	public static bool IsHttpUrl(string? url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
	}

	public static bool IsPlatform(string? platform)
	{
		return platform is not null && new[] { "bluesky", "mastodon", "linkedin" }.Contains(platform, StringComparer.OrdinalIgnoreCase);
	}
}

public sealed class ListScheduledPostsValidator : Validator<ListScheduledPostsRequest>
{
	public ListScheduledPostsValidator()
	{
		RuleFor(r => r.Status).Must(status => status is null || Enum.GetNames<ScheduledPostStatus>().Contains(status, StringComparer.OrdinalIgnoreCase))
			.WithMessage("Status must be Pending, Processing, Published, Failed, NeedsReview, or Cancelled.");
		RuleFor(r => r.PageSize).InclusiveBetween(1, 100);
		RuleFor(r => r.ContinuationToken).MaximumLength(8_192);
		RuleFor(r => r.To).Must((r, to) => !r.From.HasValue || !to.HasValue || r.From <= to)
			.WithMessage("To must be at or after From.");
	}
}

public sealed class GetScheduledPostValidator : Validator<GetScheduledPostRequest>
{
	public GetScheduledPostValidator()
	{
		ScheduledPostValidation.RequireIdentity(this, r => r.Id);
	}
}

public sealed class UpdateScheduledPostValidator : Validator<UpdateScheduledPostRequest>
{
	public UpdateScheduledPostValidator()
	{
		ScheduledPostValidation.RequireVersion(this, r => r.Id, r => r.Version);
		RuleFor(r => r.Text).MaximumLength(10_000);
		RuleFor(r => r.Hashtags).Must(tags => tags is null || tags.Count <= 100);
		RuleForEach(r => r.Hashtags).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(100)
			.Must(tag => tag is not null && !tag.Any(char.IsWhiteSpace));
		RuleFor(r => r.Platforms).Must(platforms => platforms is null || (platforms.Count is >= 1 and <= 3
			&& platforms.All(ScheduledPostValidation.IsPlatform)
			&& platforms.Distinct(StringComparer.OrdinalIgnoreCase).Count() == platforms.Count));
		RuleFor(r => r.Images).Must(images => images is null || images.Count <= 4);
		RuleForEach(r => r.Images).Cascade(CascadeMode.Stop).NotNull().ChildRules(image =>
		{
			image.RuleFor(i => i.Url).NotEmpty().MaximumLength(2_048).Must(ScheduledPostValidation.IsHttpUrl);
			image.RuleFor(i => i.AltText).NotEmpty().MaximumLength(1_500);
		});
		RuleFor(r => r).Must(r => r.Text is not null || r.Hashtags is not null || r.Platforms is not null
			|| r.Images is not null || r.AutoThread is not null || r.ScheduledFor is not null || r.RemoveUploadedImages)
			.WithMessage("Provide at least one field to change.");
	}
}

public sealed class CancelScheduledPostValidator : Validator<CancelScheduledPostRequest>
{
	public CancelScheduledPostValidator()
	{
		ScheduledPostValidation.RequireVersion(this, r => r.Id, r => r.Version);
		RuleFor(r => r.Note).NotEmpty().MaximumLength(1_000);
	}
}

public sealed class ReconcileScheduledPostValidator : Validator<ReconcileScheduledPostRequest>
{
	public ReconcileScheduledPostValidator()
	{
		ScheduledPostValidation.RequireVersion(this, r => r.Id, r => r.Version);
		RuleFor(r => r.Note).NotEmpty().MaximumLength(1_000);
		RuleFor(r => r.PublishedDeliveries).Cascade(CascadeMode.Stop).NotNull()
			.Must(deliveries => deliveries.Count is >= 1 and <= 3)
			.Must(deliveries => deliveries.All(d => d is not null)
				&& deliveries.Select(d => d.Platform).Distinct(StringComparer.OrdinalIgnoreCase).Count() == deliveries.Count);
		RuleForEach(r => r.PublishedDeliveries).Cascade(CascadeMode.Stop).NotNull().ChildRules(delivery =>
		{
			delivery.RuleFor(d => d.Platform).Must(ScheduledPostValidation.IsPlatform);
			delivery.RuleFor(d => d.PostId).NotEmpty().MaximumLength(1_000);
			delivery.RuleFor(d => d.PostUrl).MaximumLength(2_048)
				.Must(url => url is null || ScheduledPostValidation.IsHttpUrl(url));
		});
	}
}

public sealed class RetryScheduledPostValidator : Validator<RetryScheduledPostRequest>
{
	public RetryScheduledPostValidator()
	{
		ScheduledPostValidation.RequireVersion(this, r => r.Id, r => r.Version);
		RuleFor(r => r.Note).NotEmpty().MaximumLength(1_000);
		RuleFor(r => r.ConfirmNotPublished).Equal(true).WithMessage("Confirm that the remaining posts were not published.");
		RuleFor(r => r.Platforms).Cascade(CascadeMode.Stop).NotNull()
			.Must(platforms => platforms.Count is >= 1 and <= 3 && platforms.All(ScheduledPostValidation.IsPlatform)
				&& platforms.Distinct(StringComparer.OrdinalIgnoreCase).Count() == platforms.Count);
	}
}
