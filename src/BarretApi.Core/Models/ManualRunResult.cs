namespace BarretApi.Core.Models;

public enum ManualRunOutcome
{
    Completed = 0,
    NotFound = 1,
    Busy = 2
}

/// <summary>
/// The outcome of an out-of-band run request. <see cref="Run"/> is null unless
/// <see cref="Outcome"/> is <see cref="ManualRunOutcome.Completed"/>.
/// </summary>
public sealed record ManualRunResult(ManualRunOutcome Outcome, JobRunRecord? Run);
