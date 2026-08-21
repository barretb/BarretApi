using System.Text.Json;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Reads the optional JSON blob stored on a job definition. Arguments are what let one
/// handler serve several schedules — two tip-of-day jobs posting different categories, say.
/// </summary>
public static class JobArguments
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T? Deserialize<T>(string? json)
        where T : class
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    /// <summary>
    /// True when the value is absent or is a well-formed JSON object. Used by the API
    /// validators so a malformed argument blob is rejected at edit time.
    /// </summary>
    public static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
