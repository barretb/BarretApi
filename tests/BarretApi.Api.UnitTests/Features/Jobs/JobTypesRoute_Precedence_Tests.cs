using System.Text.Json;
using BarretApi.Api.Auth;
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

/// <summary>
/// FastEndpoints wires up every endpoint in the BarretApi.Api assembly through real ASP.NET
/// Core endpoint routing (via <see cref="TestServer"/>), so this proves which handler actually
/// answers a request for a given URL instead of assuming it from reading the route strings.
///
/// GET /api/jobs/types (<see cref="ListJobTypesEndpoint"/>) and GET /api/jobs/{Name}
/// (<see cref="GetJobEndpoint"/>) both match the literal URL "/api/jobs/types". ASP.NET Core's
/// endpoint routing scores a route by segment specificity, and a literal segment ("types") is
/// strictly more specific than a parameter segment ("{Name}") at the same position, so the
/// literal route wins regardless of registration order. This test exercises the real route
/// table end-to-end to confirm that holds for this app's FastEndpoints setup.
/// </summary>
public sealed class JobTypesRoute_Precedence_Tests : IAsyncDisposable
{
    private const string ApiKey = "route-precedence-test-key";

    private readonly WebApplication _app;
    private readonly TestServer _server;

    public JobTypesRoute_Precedence_Tests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var jobRepository = Substitute.For<IScheduledJobRepository>();
        jobRepository
            .GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ScheduledJobRecord
            {
                Name = callInfo.Arg<string>()!,
                DisplayName = callInfo.Arg<string>()!,
                JobType = "tip-of-day",
                CronExpression = "0 8 * * *",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

        builder.Services.AddSingleton(jobRepository);
        builder.Services.AddSingleton(new JobHandlerRegistry([new StubHandler("tip-of-day")]));
        builder.Configuration["Auth:ApiKey"] = ApiKey;
        builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
        builder.Services
            .AddAuthentication(ApiKeyAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);
        builder.Services.AddAuthorization();
        builder.Services.AddFastEndpoints(o =>
        {
            o.Assemblies = [typeof(GetJobEndpoint).Assembly];
            o.Filter = t => t == typeof(GetJobEndpoint) || t == typeof(ListJobTypesEndpoint);
        });

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseFastEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _server = _app.GetTestServer();
    }

    private sealed class StubHandler(string jobType) : IScheduledJobHandler
    {
        public string JobType { get; } = jobType;

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JobExecutionResult.Ok());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetApiJobsTypes_IsAnsweredByListJobTypesEndpoint_NotTheNameWildcard()
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, ApiKey);

        var response = await client.GetAsync("/api/jobs/types");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var payload = JsonSerializer.Deserialize<JobTypesResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        payload!.JobTypes.ShouldBe(["tip-of-day"]);
    }

    [Fact]
    public async Task GetApiJobsSomeOtherName_StillReachesTheNameWildcard()
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, ApiKey);

        var response = await client.GetAsync("/api/jobs/daily-tip");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var payload = JsonSerializer.Deserialize<JobResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        payload!.Name.ShouldBe("daily-tip");
    }
}
