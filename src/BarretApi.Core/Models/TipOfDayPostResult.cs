namespace BarretApi.Core.Models;

public sealed class TipOfDayPostResult
{
	public required TipOfDayRecord SelectedTip { get; init; }
	public required IReadOnlyList<PlatformPostResult> PlatformResults { get; init; }
	public required bool TipMarkedPosted { get; init; }
	public required DateTimeOffset AttemptedAtUtc { get; init; }
}
