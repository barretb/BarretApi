using BarretApi.Core.Interfaces;

namespace BarretApi.Core.Services;

/// <summary>
/// Maps a job definition's JobType to the handler that runs it. Constructed at
/// startup so a duplicate or blank job type fails fast rather than at tick time.
/// </summary>
public sealed class JobHandlerRegistry
{
    private readonly Dictionary<string, IScheduledJobHandler> _handlers;

    public JobHandlerRegistry(IEnumerable<IScheduledJobHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        _handlers = new Dictionary<string, IScheduledJobHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.JobType))
            {
                throw new InvalidOperationException(
                    $"Job handler {handler.GetType().Name} has a blank JobType.");
            }

            var jobType = handler.JobType.Trim();
            if (!_handlers.TryAdd(jobType, handler))
            {
                throw new InvalidOperationException(
                    $"More than one job handler is registered for job type '{jobType}'.");
            }
        }

        RegisteredTypes = [.. _handlers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<string> RegisteredTypes { get; }

    public bool IsRegistered(string? jobType)
        => !string.IsNullOrWhiteSpace(jobType) && _handlers.ContainsKey(jobType.Trim());

    public IScheduledJobHandler Resolve(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType) || !_handlers.TryGetValue(jobType.Trim(), out var handler))
        {
            throw new InvalidOperationException($"No job handler is registered for job type '{jobType}'.");
        }

        return handler;
    }
}
