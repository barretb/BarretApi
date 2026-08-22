using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobsEndpoint(
    IScheduledJobRepository jobRepository,
    ILogger<ListJobsEndpoint> logger)
    : EndpointWithoutRequest<JobListResponse>
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly ILogger<ListJobsEndpoint> _logger = logger;

    public override void Configure()
    {
        Get("/api/jobs");

        Summary(s =>
        {
            s.Summary = "List scheduled jobs";
            s.Description = "Returns every job definition with its schedule and last-run state.";
            s.Responses[200] = "The job definitions.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobs = await _jobRepository.GetAllAsync(ct);
        _logger.LogDebug("Returning {JobCount} job definitions.", jobs.Count);

        await Send.OkAsync(
            new JobListResponse { Jobs = [.. jobs.Select(JobResponseMapper.ToResponse)] },
            ct);
    }
}
