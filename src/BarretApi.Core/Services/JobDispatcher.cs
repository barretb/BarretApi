using System.Diagnostics;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

/// <summary>
/// Selects due jobs, claims them, runs their handlers, records the outcome, and
/// reschedules. Takes <see cref="TimeProvider"/> so schedule behaviour is testable
/// without waiting on the wall clock.
/// </summary>
public sealed class JobDispatcher(
    IScheduledJobRepository jobRepository,
    IJobRunRepository runRepository,
    JobHandlerRegistry handlerRegistry,
    IOptions<JobSchedulerOptions> options,
    TimeProvider timeProvider,
    ILogger<JobDispatcher> logger)
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly IJobRunRepository _runRepository = runRepository;
    private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
    private readonly JobSchedulerOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<JobDispatcher> _logger = logger;

    /// <summary>
    /// Runs every job that is due. Returns how many were executed.
    /// </summary>
    public async Task<int> RunDueJobsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var dueJobs = await _jobRepository.GetDueAsync(now, cancellationToken);
        var executedCount = 0;

        foreach (var job in dueJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (job.RunState == JobRunState.Running && !IsClaimStale(job, now))
            {
                _logger.LogInformation(
                    "Skipping job {JobName}: a run claimed at {ClaimedAt} is still in progress.",
                    job.Name,
                    job.ClaimedAtUtc);
                continue;
            }

            if (job.RunState == JobRunState.Running)
            {
                _logger.LogWarning(
                    "Reclaiming job {JobName}: its claim from {ClaimedAt} is older than the {TimeoutMinutes} minute timeout.",
                    job.Name,
                    job.ClaimedAtUtc,
                    _options.ClaimTimeoutMinutes);
            }

            var scheduledForUtc = job.NextRunUtc;

            if (!await _jobRepository.TryClaimAsync(job, _timeProvider.GetUtcNow(), cancellationToken))
            {
                _logger.LogInformation("Skipping job {JobName}: the claim was taken by another writer.", job.Name);
                continue;
            }

            executedCount++;
            await ExecuteClaimedJobAsync(job, JobTriggerType.Scheduled, scheduledForUtc, cancellationToken);
        }

        return executedCount;
    }

    private async Task<JobRunRecord> ExecuteClaimedJobAsync(
        ScheduledJobRecord job,
        JobTriggerType triggerType,
        DateTimeOffset? scheduledForUtc,
        CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var runId = $"run-{startedAt:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var stopwatch = Stopwatch.StartNew();

        var run = new JobRunRecord
        {
            RunId = runId,
            JobName = job.Name,
            JobType = job.JobType,
            TriggerType = triggerType,
            ScheduledForUtc = scheduledForUtc,
            StartedAtUtc = startedAt,
            Status = JobRunStatus.Succeeded,
            AttemptCount = 0
        };

        var fatalConfigurationError = ValidateJobConfiguration(job);
        if (fatalConfigurationError is not null)
        {
            stopwatch.Stop();
            job.IsEnabled = false;
            await CompleteRunAsync(job, run, JobRunStatus.Failed, null, fatalConfigurationError, stopwatch, cancellationToken);
            _logger.LogError(
                "Job {JobName} has been disabled because it is misconfigured: {Error}",
                job.Name,
                fatalConfigurationError);
            return run;
        }

        var context = new JobExecutionContext(
            job.Name,
            job.JobType,
            runId,
            scheduledForUtc,
            job.ArgumentsJson);

        var handler = _handlerRegistry.Resolve(job.JobType);

        run.AttemptCount = 1;
        JobExecutionResult result;
        try
        {
            result = await handler.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobName} threw during execution.", job.Name);
            result = JobExecutionResult.Fail(ex.Message);
        }

        stopwatch.Stop();

        var status = result.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
        await CompleteRunAsync(job, run, status, result.Summary, result.ErrorMessage, stopwatch, cancellationToken);
        return run;
    }

    /// <summary>
    /// A claim older than the configured timeout is assumed to belong to a process that
    /// died mid-run. A running job with no claim timestamp is stale by definition.
    /// </summary>
    private bool IsClaimStale(ScheduledJobRecord job, DateTimeOffset nowUtc)
    {
        if (job.ClaimedAtUtc is null)
        {
            return true;
        }

        return nowUtc - job.ClaimedAtUtc.Value >= TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes);
    }

    /// <summary>
    /// Returns a message when the job cannot be run at all — an unknown handler or an
    /// unparseable schedule. Both disable the job rather than looping on every tick.
    /// </summary>
    private string? ValidateJobConfiguration(ScheduledJobRecord job)
    {
        if (!_handlerRegistry.IsRegistered(job.JobType))
        {
            return $"No job handler is registered for job type '{job.JobType}'.";
        }

        if (!CronSchedule.TryParse(job.CronExpression, job.TimeZoneId, out _, out var cronError))
        {
            return cronError;
        }

        return null;
    }

    private async Task CompleteRunAsync(
        ScheduledJobRecord job,
        JobRunRecord run,
        JobRunStatus status,
        string? summary,
        string? errorMessage,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var completedAt = _timeProvider.GetUtcNow();

        run.Status = status;
        run.Summary = summary;
        run.ErrorMessage = errorMessage;
        run.CompletedAtUtc = completedAt;
        run.DurationMs = stopwatch.ElapsedMilliseconds;

        job.RunState = JobRunState.Idle;
        job.ClaimedAtUtc = null;
        job.LastRunUtc = completedAt;
        job.LastRunStatus = status;
        job.LastRunError = errorMessage;
        job.LastRunDurationMs = run.DurationMs;
        job.UpdatedAtUtc = completedAt;

        if (status == JobRunStatus.Succeeded)
        {
            job.ConsecutiveFailureCount = 0;
        }
        else
        {
            job.ConsecutiveFailureCount++;
        }

        if (run.TriggerType == JobTriggerType.Scheduled)
        {
            job.NextRunUtc = ComputeNextRun(job, completedAt);
        }

        await _runRepository.AddAsync(run, cancellationToken);
        await _jobRepository.UpdateAsync(job, cancellationToken);
    }

    /// <summary>
    /// The next occurrence after <paramref name="fromUtc"/> — deliberately not after the
    /// slot that was missed, so any number of missed occurrences collapse into one catch-up run.
    /// </summary>
    private static DateTimeOffset? ComputeNextRun(ScheduledJobRecord job, DateTimeOffset fromUtc)
    {
        return CronSchedule.TryParse(job.CronExpression, job.TimeZoneId, out var schedule, out _)
            ? schedule!.GetNextOccurrence(fromUtc)
            : null;
    }
}
