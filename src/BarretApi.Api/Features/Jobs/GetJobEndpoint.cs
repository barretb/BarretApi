using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class GetJobEndpoint(
    IScheduledJobRepository jobRepository,
    ILogger<GetJobEndpoint> logger)
    : Endpoint<GetJobRequest, JobResponse>
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly ILogger<GetJobEndpoint> _logger = logger;

    public override void Configure()
    {
        Get("/api/jobs/{Name}");

        Summary(s =>
        {
            s.Summary = "Get a scheduled job";
            s.Responses[200] = "The job definition.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[404] = "No job with that name exists.";
        });
    }

    public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
    {
        var job = await _jobRepository.GetByNameAsync(req.Name, ct);
        if (job is null)
        {
            _logger.LogInformation("Job {JobName} was not found.", req.Name);
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
    }
}
