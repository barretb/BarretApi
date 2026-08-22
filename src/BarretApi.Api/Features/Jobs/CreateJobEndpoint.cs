using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class CreateJobEndpoint(
    IScheduledJobRepository jobRepository,
    JobHandlerRegistry handlerRegistry,
    TimeProvider timeProvider,
    ILogger<CreateJobEndpoint> logger)
    : Endpoint<SaveJobRequest, JobResponse>
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<CreateJobEndpoint> _logger = logger;

    public override void Configure()
    {
        Post("/api/jobs");

        Summary(s =>
        {
            s.Summary = "Create a scheduled job";
            s.Description = "Validates the schedule before storing it and returns the computed next run time.";
            s.Responses[200] = "The job was created.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[409] = "A job with that name already exists.";
        });
    }

    public override async Task HandleAsync(SaveJobRequest req, CancellationToken ct)
    {
        if (!JobRequestValidation.TryBuildSchedule(req, _handlerRegistry, out var schedule, out var jobTypeError, out var cronError, out var timeZoneError))
        {
            if (jobTypeError is not null)
            {
                AddError(r => r.JobType, jobTypeError);
            }

            if (cronError is not null)
            {
                AddError(r => r.CronExpression, cronError);
            }

            if (timeZoneError is not null)
            {
                AddError(r => r.TimeZoneId, timeZoneError);
            }

            await Send.ErrorsAsync(400, ct);
            return;
        }

        var existing = await _jobRepository.GetByNameAsync(req.Name, ct);
        if (existing is not null)
        {
            await Send.ResponseAsync(JobResponseMapper.ToResponse(existing), 409, ct);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var job = new ScheduledJobRecord
        {
            Name = req.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Name.Trim() : req.DisplayName.Trim(),
            JobType = req.JobType.Trim(),
            CronExpression = req.CronExpression.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(req.TimeZoneId) ? "UTC" : req.TimeZoneId.Trim(),
            ArgumentsJson = req.ArgumentsJson,
            IsEnabled = req.IsEnabled,
            MaxRetryCount = req.MaxRetryCount,
            RetryBaseDelaySeconds = req.RetryBaseDelaySeconds,
            NextRunUtc = JobRequestValidation.ComputeNextRun(req, schedule, now),
            RunState = JobRunState.Idle,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _jobRepository.CreateAsync(job, ct);
        _logger.LogInformation(
            "Created job {JobName} ({JobType}); next run {NextRunUtc}.",
            job.Name,
            job.JobType,
            job.NextRunUtc);

        await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
    }
}
