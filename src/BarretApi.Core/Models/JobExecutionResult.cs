namespace BarretApi.Core.Models;

/// <summary>
/// The outcome of a single handler execution. Handlers report failure by
/// returning <see cref="Fail"/> or by throwing; the dispatcher treats both alike.
/// </summary>
public sealed class JobExecutionResult
{
    private JobExecutionResult(bool success, string? summary, string? errorMessage)
    {
        Success = success;
        Summary = summary;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public string? Summary { get; }
    public string? ErrorMessage { get; }

    public static JobExecutionResult Ok(string? summary = null) => new(true, summary, null);

    public static JobExecutionResult Fail(string errorMessage, string? summary = null)
        => new(false, summary, errorMessage);
}
