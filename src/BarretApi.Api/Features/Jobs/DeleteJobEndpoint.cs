using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class DeleteJobEndpoint(
    IScheduledJobRepository jobRepository,
    ILogger<DeleteJobEndpoint> logger)
    : Endpoint<GetJobRequest>
{
    private readonly IScheduledJobRepository _jobRepository = jobRepository;
    private readonly ILogger<DeleteJobEndpoint> _logger = logger;

    public override void Configure()
    {
        Delete("/api/jobs/{Name}");

        Summary(s =>
        {
            s.Summary = "Delete a scheduled job";
            s.Description = "Removes the job definition. Its run history is left in place and ages out with the retention window.";
            s.Responses[204] = "The job was deleted.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[404] = "No job with that name exists.";
        });
    }

    public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
    {
        var job = await _jobRepository.GetByNameAsync(req.Name, ct);
        if (job is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _jobRepository.DeleteAsync(req.Name, ct);
        _logger.LogInformation("Deleted job {JobName}.", req.Name);

        await Send.NoContentAsync(ct);
    }
}
