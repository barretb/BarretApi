using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a NASA GIBS satellite snapshot. Every argument is optional; anything left null
/// falls back to the NasaGibs configuration, including the default bounding box.
/// </summary>
public sealed class SatelliteJobHandler(
    INasaGibsPostService nasaGibsPostService,
    ILogger<SatelliteJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly INasaGibsPostService _nasaGibsPostService = nasaGibsPostService;
    private readonly ILogger<SatelliteJobHandler> _logger = logger;

    public sealed record Arguments(
        string? Layer,
        string? Title,
        string? Description,
        string[]? Platforms,
        double? BboxSouth,
        double? BboxWest,
        double? BboxNorth,
        double? BboxEast,
        int? ImageWidth,
        int? ImageHeight);

    public string JobType => "satellite";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

        var result = await _nasaGibsPostService.PostAsync(
            null,
            arguments?.Layer,
            arguments?.Title,
            arguments?.Description,
            arguments?.BboxSouth,
            arguments?.BboxWest,
            arguments?.BboxNorth,
            arguments?.BboxEast,
            arguments?.ImageWidth,
            arguments?.ImageHeight,
            arguments?.Platforms ?? [],
            cancellationToken);

        _logger.LogInformation(
            "Satellite snapshot {Layer} for {Date} posted for job {JobName}.",
            result.Layer,
            result.Date,
            context.JobName);

        return PlatformResultSummary.ToResult(result.PlatformResults, result.Title);
    }
}
