using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobRunsEndpoint(
    IScheduledJobRepository jobRepository,
    IJobRunRepository runRepository,
    ILogger<ListJobRunsEndpoint> logger)
    : Endpoint<ListJobRunsRequest, JobRunListResponse>
{
    private const int DefaultMaxCount = 50;
    private const int MaximumMaxCount = 500;

    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly IJobRunRepository _runRepository = runRepository;
    private readonly ILogger<ListJobRunsEndpoint> _logger = logger;

    public override void Configure()
    {
        Get("/api/jobs/{Name}/runs");

        Summary(s =>
        {
            s.Summary = "List a job's run history";
            s.Description = "Returns runs newest first. Runs age out after the configured retention window.";
            s.Responses[200] = "The run history.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[404] = "No job with that name exists.";
        });
    }

    public override async Task HandleAsync(ListJobRunsRequest req, CancellationToken ct)
    {
        var job = await _jobRepository.GetByNameAsync(req.Name, ct);
        if (job is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var maxCount = Math.Clamp(req.MaxCount ?? DefaultMaxCount, 1, MaximumMaxCount);
        var runs = await _runRepository.GetByJobAsync(req.Name, maxCount, ct);

        _logger.LogDebug("Returning {RunCount} runs for job {JobName}.", runs.Count, req.Name);

        await Send.OkAsync(
            new JobRunListResponse { Runs = [.. runs.Select(JobRunResponse.FromRecord)] },
            ct);
    }
}
