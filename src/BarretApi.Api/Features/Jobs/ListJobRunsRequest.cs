namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobRunsRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// How many runs to return, newest first. Defaults to 50 and is clamped to 500.
    /// </summary>
    public int? MaxCount { get; set; }
}
