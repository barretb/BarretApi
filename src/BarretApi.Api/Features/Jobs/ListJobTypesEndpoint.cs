using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobTypesEndpoint(JobHandlerRegistry handlerRegistry)
    : EndpointWithoutRequest<JobTypesResponse>
{
    private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;

    public override void Configure()
    {
        Get("/api/jobs/types");

        Summary(s =>
        {
            s.Summary = "List registered job types";
            s.Description = "The values accepted for a job definition's jobType. Adding a new one requires a code change and a deploy.";
            s.Responses[200] = "The registered job types.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(
            new JobTypesResponse { JobTypes = _handlerRegistry.RegisteredTypes },
            ct);
    }
}
