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
        // The route segment is the job's identity, not whatever "Name" the body carries.
        // Reading it explicitly (rather than relying on req.Name, which model-binding
        // happens to populate from the route today) keeps the lookup correct even if
        // route-vs-body binding precedence ever changes.
        var name = Route<string>("Name");

        if (!JobRequestValidation.TryBuildSchedule(req, _handlerRegistry, out var schedule, out var jobTypeError, out var scheduleError))
        {
            if (jobTypeError is not null)
            {
                AddError(r => r.JobType, jobTypeError);
            }

            if (scheduleError is not null)
            {
                AddError(r => r.CronExpression, scheduleError);
            }
        }

        if (ValidationFailed)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var job = await _jobRepository.GetByNameAsync(name!, ct);
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
            job.NextRunUtc = JobRequestValidation.ComputeNextRun(req, schedule!, now);
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
