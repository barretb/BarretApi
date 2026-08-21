namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Addresses a single job by its slug name. Shared by get, delete, manual run, and run history.
/// </summary>
public sealed class GetJobRequest
{
    public string Name { get; set; } = string.Empty;
}
