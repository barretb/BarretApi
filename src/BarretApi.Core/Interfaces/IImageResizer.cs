namespace BarretApi.Core.Interfaces;

/// <summary>
/// Abstracts image resizing to fit within byte size limits.
/// </summary>
public interface IImageResizer
{
	/// <summary>
	/// Resizes an image (if needed) to fit within the specified byte limit.
	/// Returns the original bytes if already within limit.
	/// Output is always JPEG format.
	/// </summary>
	byte[] ResizeToFit(byte[] imageBytes, long maxBytes);
}
