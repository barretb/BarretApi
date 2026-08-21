using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts the NASA Astronomy Picture of the Day. The date is always left null so the
/// service fetches today's entry — a scheduled run never backfills an older one.
/// </summary>
public sealed class NasaApodJobHandler(
    INasaApodPostService nasaApodPostService,
    ILogger<NasaApodJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly INasaApodPostService _nasaApodPostService = nasaApodPostService;
    private readonly ILogger<NasaApodJobHandler> _logger = logger;

    public sealed record Arguments(string[]? Platforms);

    public string JobType => "nasa-apod";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

        var result = await _nasaApodPostService.PostAsync(
            null,
            arguments?.Platforms ?? [],
            cancellationToken);

        _logger.LogInformation(
            "APOD {Title} ({Date}) posted for job {JobName}.",
            result.ApodEntry.Title,
            result.ApodEntry.Date,
            context.JobName);

        return PlatformResultSummary.ToResult(result.PlatformResults, result.ApodEntry.Title);
    }
}
