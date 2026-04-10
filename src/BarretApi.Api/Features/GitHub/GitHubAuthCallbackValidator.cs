using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubAuthCallbackValidator : Validator<GitHubAuthCallbackRequest>
{
    public GitHubAuthCallbackValidator()
    {
        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("State parameter is required.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.Error))
            .WithMessage("Authorization code is required when no error is present.");
    }
}
