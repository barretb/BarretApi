namespace BarretApi.Core.Models;

public sealed class LinkedInTokenRecord
{
	public required string AccessToken { get; init; }
	public required string RefreshToken { get; init; }
	public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }
	public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }
	public DateTimeOffset UpdatedAtUtc { get; init; }
}
