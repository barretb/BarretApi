using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class RunJobEndpoint(
    JobDispatcher dispatcher,
    ILogger<RunJobEndpoint> logger)
    : Endpoint<GetJobRequest, JobRunResponse>
{
    private readonly JobDispatcher _dispatcher = dispatcher;
    private readonly ILogger<RunJobEndpoint> _logger = logger;

    public override void Configure()
    {
        Post("/api/jobs/{Name}/run");

        Summary(s =>
        {
            s.Summary = "Run a job immediately";
            s.Description = "Runs the job out of band without changing its next run time. Works on a disabled job. Returns 409 if a run is already in progress.";
            s.Responses[200] = "The run completed successfully.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[404] = "No job with that name exists.";
            s.Responses[409] = "A run is already in progress for this job.";
            s.Responses[502] = "The run completed but the job failed.";
        });
    }

    public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
    {
        var result = await _dispatcher.RunManuallyAsync(req.Name, ct);

        switch (result.Outcome)
        {
            case ManualRunOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                return;

            case ManualRunOutcome.Busy:
                _logger.LogInformation("Manual run of job {JobName} was rejected: already running.", req.Name);
                AddError(r => r.Name, "A run is already in progress for this job.");
                await Send.ErrorsAsync(409, ct);
                return;

            default:
                var run = result.Run!;
                var statusCode = run.Status == JobRunStatus.Succeeded ? 200 : 502;
                await Send.ResponseAsync(JobRunResponse.FromRecord(run), statusCode, ct);
                return;
        }
    }
}
