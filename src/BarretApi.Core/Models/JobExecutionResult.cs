namespace BarretApi.Core.Models;

/// <summary>
/// The outcome of a single handler execution. Handlers report failure by
/// returning <see cref="Fail"/> or by throwing; the dispatcher treats both alike.
/// </summary>
public sealed class JobExecutionResult
{
    private JobExecutionResult(bool success, string? summary, string? errorMessage, bool isPartialSuccess)
    {
        Success = success;
        Summary = summary;
        ErrorMessage = errorMessage;
        IsPartialSuccess = isPartialSuccess;
    }

    public bool Success { get; }
    public string? Summary { get; }
    public string? ErrorMessage { get; }

    /// <summary>
    /// True when <see cref="Success"/> is true but the handler still recorded a problem
    /// worth surfacing (for example, a multi-platform publish where some platforms
    /// failed). The dispatcher does not retry a partial success — retrying would
    /// re-post content that already published on the platforms that succeeded — but it
    /// must still carry <see cref="ErrorMessage"/> into the job's <c>LastRunError</c>
    /// and keep incrementing <c>ConsecutiveFailureCount</c> rather than resetting it,
    /// so the problem stays visible instead of looking like a fully healthy run.
    /// </summary>
    public bool IsPartialSuccess { get; }

    public static JobExecutionResult Ok(string? summary = null) => new(true, summary, null, false);

    public static JobExecutionResult Fail(string errorMessage, string? summary = null)
        => new(false, summary, errorMessage, false);

    /// <summary>
    /// Succeeded overall — no retry — but with a failure detail worth recording. Use
    /// this instead of <see cref="Fail"/> when re-running the handler would duplicate
    /// work that already completed (e.g. some platforms in a multi-platform publish
    /// already posted).
    /// </summary>
    public static JobExecutionResult PartialSuccess(string summary, string failureDetail)
        => new(true, summary, failureDetail, true);
}
