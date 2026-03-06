using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class RssRandomPostValidator : Validator<RssRandomPostRequest>
{
	public RssRandomPostValidator()
	{
		RuleFor(x => x.FeedUrl)
			.NotEmpty()
			.WithMessage("Feed URL is required.");

		RuleFor(x => x.FeedUrl)
			.Must(BeAValidAbsoluteHttpUrl!)
			.When(x => !string.IsNullOrWhiteSpace(x.FeedUrl))
			.WithMessage("Feed URL must be a valid absolute URL with http or https scheme.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be 'bluesky', 'mastodon', or 'linkedin'.");

		RuleFor(x => x.MaxAgeDays)
			.GreaterThan(0)
			.When(x => x.MaxAgeDays.HasValue)
			.WithMessage("MaxAgeDays must be greater than 0 when provided.");
	}

	private static bool BeAValidAbsoluteHttpUrl(string url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out var uri)
			&& uri.Scheme is "http" or "https";
	}
}
