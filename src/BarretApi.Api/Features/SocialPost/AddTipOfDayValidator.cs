using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class AddTipOfDayValidator : Validator<AddTipOfDayRequest>
{
	public AddTipOfDayValidator()
	{
		RuleFor(x => x.Category)
			.NotEmpty()
			.WithMessage("Category is required.")
			.MaximumLength(100)
			.WithMessage("Category must not exceed 100 characters.");

		RuleFor(x => x.Tip)
			.NotEmpty()
			.WithMessage("Tip is required.")
			.MaximumLength(2_000)
			.WithMessage("Tip must not exceed 2,000 characters.");

		RuleFor(x => x.MoreInfoUrl)
			.Must(BeAValidAbsoluteHttpUrl!)
			.When(x => !string.IsNullOrWhiteSpace(x.MoreInfoUrl))
			.WithMessage("MoreInfoUrl must be a valid absolute URL with http or https scheme.");
	}

	private static bool BeAValidAbsoluteHttpUrl(string url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out var uri)
			&& uri.Scheme is "http" or "https";
	}
}
