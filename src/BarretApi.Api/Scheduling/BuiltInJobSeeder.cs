using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;

namespace BarretApi.Api.Scheduling;

/// <summary>
/// Creates the job definitions the scheduler ships with. Existing rows are never
/// overwritten, so pausing or retuning a built-in job survives a restart.
/// </summary>
public sealed class BuiltInJobSeeder(
    IScheduledJobRepository jobRepository,
    TimeProvider timeProvider,
    ILogger<BuiltInJobSeeder> logger)
{
    private const string PurgeJobName = "purge-job-runs";
    private const string PurgeCronExpression = "0 3 * * *";

    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<BuiltInJobSeeder> _logger = logger;

    public Task SeedAsync(CancellationToken cancellationToken = default)
        => SeedAsync(PurgeCronExpression, cancellationToken);

    /// <summary>
    /// Overload accepting the cron expression directly so tests can exercise the
    /// unparseable-expression path without touching the hardcoded default.
    /// </summary>
    internal async Task SeedAsync(string cronExpression, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _jobRepository.GetByNameAsync(PurgeJobName, cancellationToken);
            if (existing is not null)
            {
                return;
            }

            if (!CronSchedule.TryParse(cronExpression, "UTC", out var schedule, out var parseError))
            {
                // The cron expression is normally a hardcoded constant, so this only fires if
                // it is ever mistyped. Without this, the job would be created enabled with a
                // null NextRunUtc and would then never be picked up by GetDueAsync — a job
                // that silently never runs. Skip creation instead and say why, loudly, so the
                // next restart keeps retrying and keeps logging until it is fixed.
                _logger.LogError(
                    "Built-in job {JobName} has an unparseable cron expression '{CronExpression}': {ParseError}. Skipping seed.",
                    PurgeJobName,
                    cronExpression,
                    parseError);
                return;
            }

            var now = _timeProvider.GetUtcNow();

            var job = new ScheduledJobRecord
            {
                Name = PurgeJobName,
                DisplayName = "Purge job run history",
                JobType = PurgeJobName,
                CronExpression = cronExpression,
                TimeZoneId = "UTC",
                IsEnabled = true,
                NextRunUtc = schedule?.GetNextOccurrence(now),
                MaxRetryCount = 1,
                RetryBaseDelaySeconds = 30,
                RunState = JobRunState.Idle,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _jobRepository.CreateAsync(job, cancellationToken);
            _logger.LogInformation("Seeded the built-in {JobName} job; next run {NextRunUtc}.", job.Name, job.NextRunUtc);
        }
        catch (OperationCanceledException ex)
        {
            // The startup call site bounds this with a short timeout (see Program.cs) so a
            // storage outage cannot delay the app becoming healthy. A timeout here is
            // expected-ish rather than a bug, so it is a warning rather than an error.
            _logger.LogWarning(ex, "Seeding the built-in jobs timed out or was cancelled. The API will start without them.");
        }
        catch (Exception ex)
        {
            // Seeding is a convenience. A storage outage must not stop the API from starting;
            // the job can be created by hand through POST /api/jobs.
            _logger.LogError(ex, "Failed to seed built-in jobs. The API will start without them.");
        }
    }
}
