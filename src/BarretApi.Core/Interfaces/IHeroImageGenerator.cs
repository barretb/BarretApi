using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Generates a branded hero image as a PNG byte array.
/// </summary>
public interface IHeroImageGenerator
{
	/// <summary>
	/// Composites and renders a hero image from the supplied command inputs.
	/// </summary>
	/// <param name="command">Title, optional subtitle, and optional custom background bytes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A byte array containing the PNG image data (1280×720).</returns>
	Task<byte[]> GenerateAsync(HeroImageGenerationCommand command, CancellationToken cancellationToken = default);
}
