using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.WordCloud;

public sealed class GenerateWordCloudValidator : Validator<GenerateWordCloudRequest>
{
    public GenerateWordCloudValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("URL is required.");

        RuleFor(x => x.Url)
            .Must(BeAValidAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("The URL must be a valid absolute HTTP or HTTPS URL.");

        RuleFor(x => x.Width)
            .InclusiveBetween(200, 2000)
            .When(x => x.Width.HasValue)
            .WithMessage("Width must be between 200 and 2000 pixels.");

        RuleFor(x => x.Height)
            .InclusiveBetween(200, 2000)
            .When(x => x.Height.HasValue)
            .WithMessage("Height must be between 200 and 2000 pixels.");
    }

    private static bool BeAValidAbsoluteHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
