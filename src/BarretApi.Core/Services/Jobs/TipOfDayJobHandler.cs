using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a tip from one category. The category comes from the job's arguments, so several
/// tip-of-day jobs can run on different schedules against different categories.
/// </summary>
public sealed class TipOfDayJobHandler(
    ITipOfDayService tipOfDayService,
    ILogger<TipOfDayJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly ITipOfDayService _tipOfDayService = tipOfDayService;
    private readonly ILogger<TipOfDayJobHandler> _logger = logger;

    public sealed record Arguments(string? Category, string[]? Platforms, string? Leader);

    public string JobType => "tip-of-day";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

        if (string.IsNullOrWhiteSpace(arguments?.Category))
        {
            return JobExecutionResult.Fail(
                "A tip-of-day job requires a category in its arguments, for example {\"category\":\"dotnet\"}.");
        }

        var command = new TipOfDayPostCommand
        {
            Category = arguments.Category,
            Platforms = arguments.Platforms ?? [],
            Leader = arguments.Leader
        };

        var result = await _tipOfDayService.SelectAndPostAsync(command, cancellationToken);

        _logger.LogInformation(
            "Tip {TipId} selected for job {JobName} in category {Category}.",
            result.SelectedTip.TipId,
            context.JobName,
            command.Category);

        return PlatformResultSummary.ToResult(result.PlatformResults, result.SelectedTip.Tip);
    }
}
