using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Generates letterboard-style signboard PNG images from text.
/// </summary>
public interface ISignboardGenerator
{
	Task<byte[]> GenerateAsync(SignboardGenerationCommand command, CancellationToken cancellationToken = default);
}
