using System.Diagnostics.CodeAnalysis;
using BarretApi.Core.Services;

namespace BarretApi.Api.Features.Jobs;

internal static class JobRequestValidation
{
    /// <summary>
    /// Validates the job type is registered and the cron expression/time zone parse,
    /// parsing the schedule exactly once. Returns true only when both checks pass, in
    /// which case <paramref name="schedule"/> is the parsed result. Callers add the
    /// returned error messages against the field the error concerns
    /// (<c>r.JobType</c> or <c>r.CronExpression</c>) so client responses point at the
    /// offending field.
    /// </summary>
    public static bool TryBuildSchedule(
        SaveJobRequest request,
        JobHandlerRegistry handlerRegistry,
        [NotNullWhen(true)] out CronSchedule? schedule,
        out string? jobTypeError,
        out string? scheduleError)
    {
        jobTypeError = handlerRegistry.IsRegistered(request.JobType)
            ? null
            : $"'{request.JobType}' is not a registered job type. Call GET /api/jobs/types for the list.";

        var scheduleValid = CronSchedule.TryParse(request.CronExpression, request.TimeZoneId, out schedule, out scheduleError);
        if (!scheduleValid)
        {
            scheduleError ??= "The schedule is not valid.";
        }

        return jobTypeError is null && scheduleValid;
    }

    /// <summary>
    /// The next occurrence after <paramref name="fromUtc"/> for an already-parsed
    /// schedule, or null when the job is disabled. Callers validate the schedule with
    /// <see cref="TryBuildSchedule"/> first and pass in the resulting <see cref="CronSchedule"/>
    /// so the cron expression is not parsed a second time.
    /// </summary>
    public static DateTimeOffset? ComputeNextRun(
        SaveJobRequest request,
        CronSchedule schedule,
        DateTimeOffset fromUtc)
    {
        return request.IsEnabled ? schedule.GetNextOccurrence(fromUtc) : null;
    }
}
