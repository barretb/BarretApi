using System.Text.RegularExpressions;
using BarretApi.Core.Services.Jobs;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Shape-only validation. Rules that need services — is the job type registered, does the
/// cron parse — run in the endpoint, which can resolve them.
/// </summary>
public sealed partial class SaveJobValidator : Validator<SaveJobRequest>
{
    public SaveJobValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.")
            .Must(name => SlugPattern().IsMatch(name))
            .WithMessage("Name must be a slug: lowercase letters, digits, and single hyphens between them.");

        RuleFor(r => r.DisplayName)
            .MaximumLength(200).WithMessage("DisplayName must be 200 characters or fewer.");

        RuleFor(r => r.JobType)
            .NotEmpty().WithMessage("JobType is required.")
            .MaximumLength(100).WithMessage("JobType must be 100 characters or fewer.");

        RuleFor(r => r.CronExpression)
            .NotEmpty().WithMessage("CronExpression is required.")
            .MaximumLength(200).WithMessage("CronExpression must be 200 characters or fewer.");

        RuleFor(r => r.TimeZoneId)
            .MaximumLength(100).WithMessage("TimeZoneId must be 100 characters or fewer.");

        RuleFor(r => r.MaxRetryCount)
            .InclusiveBetween(0, 10).WithMessage("MaxRetryCount must be between 0 and 10.");

        RuleFor(r => r.RetryBaseDelaySeconds)
            .InclusiveBetween(0, 3_600).WithMessage("RetryBaseDelaySeconds must be between 0 and 3600.");

        RuleFor(r => r.ArgumentsJson)
            .Must(JobArguments.IsValidJson)
            .WithMessage("ArgumentsJson must be a JSON object.");
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
