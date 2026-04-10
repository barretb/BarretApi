using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.HeroImage;

public sealed class GenerateHeroImageValidator : Validator<GenerateHeroImageRequest>
{
	private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"image/jpeg",
		"image/png"
	};

	private const long MaxBackgroundBytes = 10_485_760; // 10 MB

	public GenerateHeroImageValidator()
	{
		RuleFor(x => x.Title)
			.NotEmpty()
			.WithMessage("Title is required.");

		RuleFor(x => x.Title)
			.MaximumLength(200)
			.When(x => !string.IsNullOrWhiteSpace(x.Title))
			.WithMessage("Title must not exceed 200 characters.");

		RuleFor(x => x.Subtitle)
			.MaximumLength(300)
			.When(x => !string.IsNullOrWhiteSpace(x.Subtitle))
			.WithMessage("Subtitle must not exceed 300 characters.");

		RuleFor(x => x.BackgroundImage!.ContentType)
			.Must(ct => AllowedContentTypes.Contains(ct))
			.When(x => x.BackgroundImage is not null)
			.WithMessage("Background image must be JPEG or PNG format.");

		RuleFor(x => x.BackgroundImage!.Length)
			.LessThanOrEqualTo(MaxBackgroundBytes)
			.When(x => x.BackgroundImage is not null)
			.WithMessage("Background image must not exceed 10 MB.");
	}
}
