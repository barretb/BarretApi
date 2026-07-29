namespace BarretApi.Core.Models;

public sealed record SignboardPostResult(
	int Width,
	int Height,
	int Seed,
	bool ImageAttached,
	IReadOnlyList<PlatformPostResult> PlatformResults);
