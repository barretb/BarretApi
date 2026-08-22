using BarretApi.Core.Configuration;
using BarretApi.Core.Services;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Scheduling;

/// <summary>
/// Ticks on an interval and asks the dispatcher to run whatever is due. Holds no state:
/// if the process dies mid-run, the next tick recovers from what is in storage.
/// </summary>
public sealed class JobSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<JobSchedulerOptions> options,
    ILogger<JobSchedulerHostedService> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly JobSchedulerOptions _options = options.Value;
    private readonly ILogger<JobSchedulerHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Job scheduler is disabled (JobScheduler:Enabled is false). Job management endpoints remain available.");
            return;
        }

        _logger.LogInformation(
            "Job scheduler started with a {TickIntervalSeconds}s tick.",
            _options.TickIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.TickIntervalSeconds));

        // Run once immediately so a restart catches up anything missed while the app was down.
        await RunTickAsync(stoppingToken);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job scheduler is stopping.");
        }
    }

    /// <summary>
    /// One pass over the due jobs. Never throws: a storage outage must not tear down the
    /// loop, because nothing would restart it short of an app restart.
    /// </summary>
    internal async Task RunTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<JobDispatcher>();

            var executedCount = await dispatcher.RunDueJobsAsync(cancellationToken);

            if (executedCount > 0)
            {
                _logger.LogInformation("Job scheduler tick executed {ExecutedCount} job(s).", executedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job scheduler tick failed. The loop will continue.");
        }
    }
}
