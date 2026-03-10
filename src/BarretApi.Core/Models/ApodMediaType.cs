namespace BarretApi.Core.Models;

/// <summary>
/// Represents the media type of a NASA Astronomy Picture of the Day.
/// </summary>
public enum ApodMediaType
{
    /// <summary>
    /// Static image (JPEG, PNG, etc.).
    /// </summary>
    Image,

    /// <summary>
    /// Video embed (typically YouTube).
    /// </summary>
    Video
}
