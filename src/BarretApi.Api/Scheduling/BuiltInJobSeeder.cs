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

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _jobRepository.GetByNameAsync(PurgeJobName, cancellationToken);
            if (existing is not null)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            CronSchedule.TryParse(PurgeCronExpression, "UTC", out var schedule, out _);

            var job = new ScheduledJobRecord
            {
                Name = PurgeJobName,
                DisplayName = "Purge job run history",
                JobType = PurgeJobName,
                CronExpression = PurgeCronExpression,
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
        catch (Exception ex)
        {
            // Seeding is a convenience. A storage outage must not stop the API from starting;
            // the job can be created by hand through POST /api/jobs.
            _logger.LogError(ex, "Failed to seed built-in jobs. The API will start without them.");
        }
    }
}
