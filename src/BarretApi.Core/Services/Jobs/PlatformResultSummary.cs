using BarretApi.Core.Models;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Turns platform post results into a job outcome. Handlers have no per-platform
/// idempotency, so retrying a run that already published to some platforms would
/// duplicate that content. A run only fails — and is therefore retried — when
/// nothing published anywhere; a partial publish is reported as a success whose
/// summary names the platforms that still failed.
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

        var failureDetail = string.Join(
            "; ",
            failures.Select(f => $"{f.Platform}: {f.ErrorMessage ?? f.ErrorCode ?? "failed"}"));

        if (failures.Count == results.Count)
        {
            return JobExecutionResult.Fail($"Failed to post \"{subject}\" — {failureDetail}", $"Posted \"{subject}\".");
        }

        var succeeded = results.Where(r => r.Success).ToList();
        var succeededPlatforms = string.Join(", ", succeeded.Select(r => r.Platform));

        return JobExecutionResult.Ok(
            $"Posted \"{subject}\" to {succeededPlatforms}. Failed on {failureDetail}. Not retried to avoid duplicate posts on the platforms that already succeeded.");
    }
}
