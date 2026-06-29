using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class PostTipOfDayValidator : Validator<PostTipOfDayRequest>
{
	public PostTipOfDayValidator()
	{
		RuleFor(x => x.Category)
			.NotEmpty()
			.WithMessage("Category is required.")
			.MaximumLength(100)
			.WithMessage("Category must not exceed 100 characters.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be 'bluesky', 'mastodon', or 'linkedin'.");

		RuleFor(x => x.Leader)
			.MaximumLength(500)
			.When(x => x.Leader is not null)
			.WithMessage("Leader must not exceed 500 characters.");
	}
}
