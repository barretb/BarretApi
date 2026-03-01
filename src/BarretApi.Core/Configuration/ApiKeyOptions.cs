namespace BarretApi.Core.Configuration;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Auth";

    public required string ApiKey { get; init; }
}
