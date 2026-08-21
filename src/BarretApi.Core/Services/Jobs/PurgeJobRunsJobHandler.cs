using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Deletes run history past the retention window. Built in rather than special-cased,
/// so it appears in the job list and can be paused like any other job.
/// </summary>
public sealed class PurgeJobRunsJobHandler(
    IJobRunRepository runRepository,
    IOptions<JobSchedulerOptions> options,
    TimeProvider timeProvider,
    ILogger<PurgeJobRunsJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly IJobRunRepository _runRepository = runRepository;
    private readonly JobSchedulerOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<PurgeJobRunsJobHandler> _logger = logger;

    public string JobType => "purge-job-runs";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.RunRetentionDays);
        var deletedCount = await _runRepository.PurgeOlderThanAsync(cutoff, cancellationToken);

        _logger.LogInformation(
            "Purged {DeletedCount} job runs older than {RetentionDays} days.",
            deletedCount,
            _options.RunRetentionDays);

        return JobExecutionResult.Ok($"Deleted {deletedCount} run(s) started before {cutoff:O}.");
    }
}
