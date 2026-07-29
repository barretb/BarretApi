using BarretApi.Core.Services;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardValidator : Validator<GenerateSignboardRequest>
{
	public GenerateSignboardValidator()
	{
		RuleFor(x => x.Text)
			.Must(t => !string.IsNullOrWhiteSpace(t))
			.WithMessage("Text is required.");

		RuleFor(x => x.Text)
			.MaximumLength(200)
			.When(x => !string.IsNullOrWhiteSpace(x.Text))
			.WithMessage("Text must not exceed 200 characters.");

		RuleFor(x => x.Text)
			.Must(t => SignboardCharacterSet.FindUnsupported(t!).Count == 0)
			.When(x => !string.IsNullOrWhiteSpace(x.Text))
			.WithMessage(x =>
			{
				var unsupported = string.Join(" ", SignboardCharacterSet
					.FindUnsupported(x.Text!)
					.Select(c => $"'{c}'"));
				return $"Text contains unsupported characters: {unsupported}. "
					+ $"Supported: A-Z, 0-9, space, newline, and {SignboardCharacterSet.SupportedPunctuation}";
			});

		RuleFor(x => x.Width)
			.InclusiveBetween(400, 2000)
			.When(x => x.Width is not null)
			.WithMessage("Width must be between 400 and 2000.");

		RuleFor(x => x.Height)
			.InclusiveBetween(400, 2000)
			.When(x => x.Height is not null)
			.WithMessage("Height must be between 400 and 2000.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be one of: bluesky, mastodon, linkedin.");

		RuleFor(x => x.Caption)
			.MaximumLength(1000)
			.When(x => !string.IsNullOrWhiteSpace(x.Caption))
			.WithMessage("Caption must not exceed 1000 characters.");

		RuleFor(x => x.AltText)
			.MaximumLength(1500)
			.When(x => !string.IsNullOrWhiteSpace(x.AltText))
			.WithMessage("AltText must not exceed 1500 characters.");

		RuleForEach(x => x.Hashtags)
			.Must(h => !string.IsNullOrWhiteSpace(h) && !h.Contains(' ') && h.Length <= 100)
			.When(x => x.Hashtags is not null && x.Hashtags.Count > 0)
			.WithMessage("Each hashtag must be non-empty, contain no spaces, and not exceed 100 characters.");
	}
}
