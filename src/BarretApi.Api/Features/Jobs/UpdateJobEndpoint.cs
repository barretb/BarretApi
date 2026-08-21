using BarretApi.Core.Interfaces;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class UpdateJobEndpoint(
    IScheduledJobRepository jobRepository,
    JobHandlerRegistry handlerRegistry,
    TimeProvider timeProvider,
    ILogger<UpdateJobEndpoint> logger)
    : Endpoint<SaveJobRequest, JobResponse>
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<UpdateJobEndpoint> _logger = logger;

    public override void Configure()
    {
        Put("/api/jobs/{Name}");

        Summary(s =>
        {
            s.Summary = "Update a scheduled job";
            s.Description = "Changing the schedule, the time zone, or enabling a paused job recomputes the next run time from now, so a long-paused job does not fire immediately on resume.";
            s.Responses[200] = "The job was updated.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[404] = "No job with that name exists.";
        });
    }

    public override async Task HandleAsync(SaveJobRequest req, CancellationToken ct)
    {
        if (!_handlerRegistry.IsRegistered(req.JobType))
        {
            AddError(
                r => r.JobType,
                $"'{req.JobType}' is not a registered job type. Call GET /api/jobs/types for the list.");
        }

        if (!CronSchedule.TryParse(req.CronExpression, req.TimeZoneId, out _, out var scheduleError))
        {
            AddError(r => r.CronExpression, scheduleError ?? "The schedule is not valid.");
        }

        if (ValidationFailed)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var job = await _jobRepository.GetByNameAsync(req.Name, ct);
        if (job is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var scheduleChanged =
            !string.Equals(job.CronExpression, req.CronExpression.Trim(), StringComparison.Ordinal)
            || !string.Equals(job.TimeZoneId, req.TimeZoneId.Trim(), StringComparison.OrdinalIgnoreCase);
        var wasEnabled = job.IsEnabled;

        job.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? job.Name : req.DisplayName.Trim();
        job.JobType = req.JobType.Trim();
        job.CronExpression = req.CronExpression.Trim();
        job.TimeZoneId = string.IsNullOrWhiteSpace(req.TimeZoneId) ? "UTC" : req.TimeZoneId.Trim();
        job.ArgumentsJson = req.ArgumentsJson;
        job.IsEnabled = req.IsEnabled;
        job.MaxRetryCount = req.MaxRetryCount;
        job.RetryBaseDelaySeconds = req.RetryBaseDelaySeconds;
        job.UpdatedAtUtc = now;

        if (!req.IsEnabled)
        {
            job.NextRunUtc = null;
        }
        else if (scheduleChanged || !wasEnabled || job.NextRunUtc is null)
        {
            job.NextRunUtc = JobRequestValidation.ComputeNextRun(req, now);
        }

        await _jobRepository.UpdateAsync(job, ct);
        _logger.LogInformation(
            "Updated job {JobName}; enabled={IsEnabled}, next run {NextRunUtc}.",
            job.Name,
            job.IsEnabled,
            job.NextRunUtc);

        await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
    }
}
