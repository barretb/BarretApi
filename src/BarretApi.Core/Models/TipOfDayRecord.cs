namespace BarretApi.Core.Models;

public sealed class TipOfDayRecord
{
	public required string TipId { get; init; }
	public required string Category { get; init; }
	public required string Tip { get; init; }
	public string? MoreInfoUrl { get; init; }
	public DateTimeOffset? LastPostedDate { get; init; }
	public DateTimeOffset CreatedAtUtc { get; init; }
}
