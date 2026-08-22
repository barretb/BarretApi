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
    ILogger<JobDispatcher> logger,
    IEmailNotificationService? emailNotificationService = null)
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly IJobRunRepository _runRepository = runRepository;
    private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
    private readonly JobSchedulerOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<JobDispatcher> _logger = logger;
    private readonly IEmailNotificationService? _emailNotificationService = emailNotificationService;

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

    /// <summary>
    /// Runs a job out of band. Does not change the job's next run time, and works on a
    /// disabled job. Returns <see cref="ManualRunOutcome.Busy"/> rather than starting a
    /// second concurrent execution.
    /// </summary>
    public async Task<ManualRunResult> RunManuallyAsync(
        string jobName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var job = await _jobRepository.GetByNameAsync(jobName, cancellationToken);
        if (job is null)
        {
            return new ManualRunResult(ManualRunOutcome.NotFound, null);
        }

        var now = _timeProvider.GetUtcNow();
        if (job.RunState == JobRunState.Running && !IsClaimStale(job, now))
        {
            return new ManualRunResult(ManualRunOutcome.Busy, null);
        }

        if (!await _jobRepository.TryClaimAsync(job, now, cancellationToken))
        {
            return new ManualRunResult(ManualRunOutcome.Busy, null);
        }

        var run = await ExecuteClaimedJobAsync(job, JobTriggerType.Manual, null, cancellationToken);
        return new ManualRunResult(ManualRunOutcome.Completed, run);
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

            if (triggerType == JobTriggerType.Scheduled)
            {
                job.IsEnabled = false;
                _logger.LogError(
                    "Job {JobName} has been disabled because it is misconfigured: {Error}",
                    job.Name,
                    fatalConfigurationError);
            }
            else
            {
                _logger.LogError(
                    "Job {JobName} is misconfigured: {Error}",
                    job.Name,
                    fatalConfigurationError);
            }

            await CompleteRunAsync(job, run, JobRunStatus.Failed, null, fatalConfigurationError, stopwatch, cancellationToken);
            await NotifyFailureAsync(job, run, cancellationToken);
            return run;
        }

        var context = new JobExecutionContext(
            job.Name,
            job.JobType,
            runId,
            scheduledForUtc,
            job.ArgumentsJson);

        var handler = _handlerRegistry.Resolve(job.JobType);

        var maxAttempts = Math.Max(0, job.MaxRetryCount) + 1;
        JobExecutionResult? result = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            run.AttemptCount = attempt;

            try
            {
                result = await handler.ExecuteAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return await AbortRunAsync(job, run, stopwatch, "during execution", cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobName} threw on attempt {Attempt}.", job.Name, attempt);
                result = JobExecutionResult.Fail(ex.Message);
            }

            if (result.Success)
            {
                break;
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    await DelayBeforeRetryAsync(job, attempt, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return await AbortRunAsync(job, run, stopwatch, "while waiting to retry", cancellationToken: CancellationToken.None);
                }
            }
        }

        stopwatch.Stop();

        var status = result!.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
        await CompleteRunAsync(
            job,
            run,
            status,
            result.Summary,
            result.ErrorMessage,
            stopwatch,
            cancellationToken,
            isPartialSuccess: result.IsPartialSuccess);

        if (status == JobRunStatus.Failed)
        {
            await NotifyFailureAsync(job, run, cancellationToken);
        }

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

    /// <summary>
    /// Records a genuine-shutdown abort and releases the claim. Called whether cancellation
    /// happens while the handler is running or while waiting out the retry backoff — either
    /// way nothing further should be attempted, and the run must still be written so the
    /// claim does not wedge until the timeout.
    /// </summary>
    private async Task<JobRunRecord> AbortRunAsync(
        ScheduledJobRecord job,
        JobRunRecord run,
        Stopwatch stopwatch,
        string reason,
        CancellationToken cancellationToken)
    {
        stopwatch.Stop();
        _logger.LogWarning("Job {JobName} was cancelled {Reason}.", job.Name, reason);
        await CompleteRunAsync(
            job,
            run,
            JobRunStatus.Aborted,
            null,
            "Execution was cancelled before it completed.",
            stopwatch,
            cancellationToken);
        return run;
    }

    private async Task CompleteRunAsync(
        ScheduledJobRecord job,
        JobRunRecord run,
        JobRunStatus status,
        string? summary,
        string? errorMessage,
        Stopwatch stopwatch,
        CancellationToken cancellationToken,
        bool isPartialSuccess = false)
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

        // A partial success is still `Succeeded` (so it is never retried), but it is not
        // a clean run — it must keep climbing the failure count rather than resetting it,
        // or a job that fails on the same platform every day would look permanently healthy.
        if (status == JobRunStatus.Succeeded && !isPartialSuccess)
        {
            job.ConsecutiveFailureCount = 0;
        }
        else
        {
            job.ConsecutiveFailureCount++;
        }

        if (run.TriggerType == JobTriggerType.Scheduled && status != JobRunStatus.Aborted)
        {
            job.NextRunUtc = ComputeNextRun(job, completedAt);
        }

        await _runRepository.AddAsync(run, cancellationToken);
        await _jobRepository.UpdateAsync(job, cancellationToken);
    }

    /// <summary>
    /// Exponential backoff between attempts. A base delay of zero disables waiting,
    /// which is what keeps the dispatcher tests fast.
    /// </summary>
    private async Task DelayBeforeRetryAsync(
        ScheduledJobRecord job,
        int attempt,
        CancellationToken cancellationToken)
    {
        if (job.RetryBaseDelaySeconds <= 0)
        {
            return;
        }

        var seconds = job.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1);
        var delay = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.FromMinutes(15).TotalSeconds));

        _logger.LogInformation(
            "Retrying job {JobName} in {DelaySeconds}s after attempt {Attempt}.",
            job.Name,
            delay.TotalSeconds,
            attempt);

        await Task.Delay(delay, _timeProvider, cancellationToken);
    }

    /// <summary>
    /// Sent once every attempt has failed (or once for a misconfigured job that never
    /// attempts at all). There is currently no rate limiting on these emails — a job
    /// that is scheduled frequently and stays broken will send one email per failed run.
    /// </summary>
    private async Task NotifyFailureAsync(
        ScheduledJobRecord job,
        JobRunRecord run,
        CancellationToken cancellationToken)
    {
        if (_emailNotificationService is null)
        {
            return;
        }

        var context = new Dictionary<string, string>
        {
            ["JobName"] = job.Name,
            ["JobType"] = job.JobType,
            ["RunId"] = run.RunId,
            ["Attempts"] = run.AttemptCount.ToString(),
            ["ConsecutiveFailures"] = job.ConsecutiveFailureCount.ToString(),
            ["NextRunUtc"] = job.NextRunUtc?.ToString("O") ?? "none"
        };

        try
        {
            await _emailNotificationService.SendPostFailureNotificationAsync(
                $"job:{job.Name}",
                run.ErrorMessage ?? "The job failed without reporting an error message.",
                context,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send a failure notification for job {JobName}.", job.Name);
        }
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
