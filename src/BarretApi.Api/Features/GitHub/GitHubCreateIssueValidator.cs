using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubCreateIssueValidator : Validator<GitHubCreateIssueRequest>
{
    public GitHubCreateIssueValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("'name' must not be empty.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("'title' must not be empty.");
    }
}
