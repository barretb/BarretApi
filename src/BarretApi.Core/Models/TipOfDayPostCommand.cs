namespace BarretApi.Core.Models;

public sealed class TipOfDayPostCommand
{
	public required string Category { get; init; }
	public IReadOnlyList<string> Platforms { get; init; } = [];
	public string? Leader { get; init; }
}
