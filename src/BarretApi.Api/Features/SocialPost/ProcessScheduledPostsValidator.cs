using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class ProcessScheduledPostsValidator : Validator<ProcessScheduledPostsRequest>
{
    public ProcessScheduledPostsValidator()
    {
        RuleFor(x => x.MaxCount)
            .GreaterThan(0)
            .When(x => x.MaxCount.HasValue)
            .WithMessage("MaxCount must be greater than 0 when provided.");

        RuleFor(x => x.MaxCount)
            .LessThanOrEqualTo(1_000)
            .When(x => x.MaxCount.HasValue)
            .WithMessage("MaxCount must be less than or equal to 1000.");
    }
}
