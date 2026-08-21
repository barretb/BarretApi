namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Body for both create and update. On update the name comes from the route and any
/// value in the body is ignored — a job cannot be renamed, because the name is its key.
/// </summary>
public sealed class SaveJobRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public string? ArgumentsJson { get; set; }
    public bool IsEnabled { get; set; }
    public int MaxRetryCount { get; set; } = 2;
    public int RetryBaseDelaySeconds { get; set; } = 30;
}
