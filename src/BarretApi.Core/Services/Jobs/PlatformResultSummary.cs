using BarretApi.Core.Models;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Turns platform post results into a job outcome. Any platform failure fails the run,
/// so a partial publish is retried and reported rather than silently accepted.
/// </summary>
internal static class PlatformResultSummary
{
    public static JobExecutionResult ToResult(
        IReadOnlyList<PlatformPostResult> results,
        string subject)
    {
        if (results.Count == 0)
        {
            return JobExecutionResult.Fail("No target platforms were configured for this job.");
        }

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count == 0)
        {
            var platforms = string.Join(", ", results.Select(r => r.Platform));
            return JobExecutionResult.Ok($"Posted \"{subject}\" to {platforms}.");
        }

        var detail = string.Join(
            "; ",
            failures.Select(f => $"{f.Platform}: {f.ErrorMessage ?? f.ErrorCode ?? "failed"}"));

        return JobExecutionResult.Fail($"Failed to post \"{subject}\" — {detail}", $"Posted \"{subject}\".");
    }
}
