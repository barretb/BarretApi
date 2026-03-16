using BarretApi.Core.Models;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Avatar;

public sealed class GenerateAvatarValidator : Validator<GenerateAvatarRequest>
{
    public GenerateAvatarValidator()
    {
        RuleFor(x => x.Style)
            .Must(style => AvatarStyle.IsValid(style))
            .When(x => x.Style is not null)
            .WithMessage("'Style' must be one of the supported styles: "
                + string.Join(", ", AvatarStyle.All) + ".");

        RuleFor(x => x.Format)
            .Must(format => AvatarFormat.IsValid(format))
            .When(x => x.Format is not null)
            .WithMessage("'Format' must be one of: "
                + string.Join(", ", AvatarFormat.All) + ".");

        RuleFor(x => x.Seed)
            .MaximumLength(256)
            .When(x => x.Seed is not null)
            .WithMessage("'Seed' must not exceed 256 characters.");
    }
}
