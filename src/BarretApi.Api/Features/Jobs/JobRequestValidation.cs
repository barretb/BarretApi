using BarretApi.Core.Services;

namespace BarretApi.Api.Features.Jobs;

internal static class JobRequestValidation
{
    /// <summary>
    /// The next occurrence after <paramref name="fromUtc"/> for a request's schedule, or
    /// null when the job is disabled. Callers validate the schedule first.
    /// </summary>
    public static DateTimeOffset? ComputeNextRun(
        SaveJobRequest request,
        DateTimeOffset fromUtc)
    {
        if (!request.IsEnabled)
        {
            return null;
        }

        return CronSchedule.TryParse(request.CronExpression, request.TimeZoneId, out var schedule, out _)
            ? schedule!.GetNextOccurrence(fromUtc)
            : null;
    }
}
