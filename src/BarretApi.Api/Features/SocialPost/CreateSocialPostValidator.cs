using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class CreateSocialPostValidator : Validator<CreateSocialPostRequest>
{
    public CreateSocialPostValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .When(x => x.Images is null || x.Images.Count == 0)
            .WithMessage("Text is required when no images are provided.");

        RuleFor(x => x.Text)
            .MaximumLength(10_000)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Text must not exceed 10,000 characters.");

        RuleFor(x => x.Platforms)
            .Must(platforms => platforms!.All(p =>
                p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("mastodon", StringComparison.OrdinalIgnoreCase)))
            .When(x => x.Platforms is not null && x.Platforms.Count > 0)
            .WithMessage("Platforms must be 'bluesky' or 'mastodon'.");

        RuleForEach(x => x.Images)
            .ChildRules(image =>
            {
                image.RuleFor(i => i.Url)
                    .NotEmpty()
                    .WithMessage("Image URL is required.");

                image.RuleFor(i => i.AltText)
                    .NotEmpty()
                    .WithMessage("Alt text is required for every image.")
                    .Must(alt => !string.IsNullOrWhiteSpace(alt))
                    .WithMessage("Alt text must not be blank or whitespace.")
                    .MaximumLength(1_500)
                    .WithMessage("Alt text must not exceed 1,500 characters.");
            })
            .When(x => x.Images is not null && x.Images.Count > 0);

        RuleFor(x => x.Images)
            .Must(images => images!.Count <= 4)
            .When(x => x.Images is not null)
            .WithMessage("Maximum of 4 images allowed.");

        RuleForEach(x => x.Hashtags)
            .MaximumLength(100)
            .WithMessage("Each hashtag must not exceed 100 characters.")
            .Must(tag => !tag.Contains(' '))
            .WithMessage("Hashtags must not contain spaces.")
            .When(x => x.Hashtags is not null && x.Hashtags.Count > 0);
    }
}
