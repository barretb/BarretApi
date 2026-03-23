using BarretApi.Core.Models;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Avatar;

public sealed class AvatarPostValidator : Validator<AvatarPostRequest>
{
	public AvatarPostValidator()
	{
		RuleFor(x => x.Style)
			.Must(style => AvatarStyle.IsValid(style))
			.When(x => x.Style is not null)
			.WithMessage("'Style' must be one of the supported styles: "
				+ string.Join(", ", AvatarStyle.All) + ".");

		RuleFor(x => x.Seed)
			.MaximumLength(256)
			.When(x => x.Seed is not null)
			.WithMessage("'Seed' must not exceed 256 characters.");

		RuleFor(x => x.Text)
			.MaximumLength(10_000)
			.When(x => !string.IsNullOrEmpty(x.Text))
			.WithMessage("'Text' must not exceed 10,000 characters.");

		RuleFor(x => x.AltText)
			.MaximumLength(1_500)
			.When(x => !string.IsNullOrEmpty(x.AltText))
			.WithMessage("'AltText' must not exceed 1,500 characters.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Platforms must be 'bluesky', 'mastodon', or 'linkedin'.");

		RuleForEach(x => x.Hashtags)
			.MaximumLength(100)
			.WithMessage("Each hashtag must not exceed 100 characters.")
			.Must(tag => !tag.Contains(' '))
			.WithMessage("Hashtags must not contain spaces.")
			.When(x => x.Hashtags is not null && x.Hashtags.Count > 0);
	}
}
