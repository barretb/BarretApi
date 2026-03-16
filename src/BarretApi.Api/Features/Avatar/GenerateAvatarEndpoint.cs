using BarretApi.Core.Interfaces;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Avatar;

public sealed class GenerateAvatarEndpoint(
    IDiceBearAvatarClient avatarClient,
    ILogger<GenerateAvatarEndpoint> logger)
    : Endpoint<GenerateAvatarRequest>
{
    private readonly IDiceBearAvatarClient _avatarClient = avatarClient;
    private readonly ILogger<GenerateAvatarEndpoint> _logger = logger;

    public override void Configure()
    {
        Get("/api/avatars/random");

        Summary(s =>
        {
            s.Summary = "Generate a random avatar image";
            s.Description = "Generates a random avatar image using the DiceBear API. "
                + "Optionally specify a style, format, and seed for customization.";
            s.ExampleRequest = new GenerateAvatarRequest
            {
                Style = "pixel-art",
                Format = "svg",
                Seed = "my-seed"
            };
            s.Responses[200] = "Avatar image generated successfully.";
            s.Responses[400] = "Request validation failed (invalid style, format, or seed).";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[502] = "The avatar generation service is temporarily unavailable.";
        });
    }

    public override async Task HandleAsync(GenerateAvatarRequest req, CancellationToken ct)
    {
        _logger.LogInformation(
            "Avatar generation requested: style={Style}, format={Format}, seed={Seed}",
            req.Style ?? "(random)", req.Format ?? "(default)", req.Seed ?? "(random)");

        try
        {
            var result = await _avatarClient.GetAvatarAsync(
                req.Style, req.Format, req.Seed, ct);

            HttpContext.Response.ContentType = result.ContentType;
            HttpContext.Response.ContentLength = result.ImageBytes.Length;
            await HttpContext.Response.Body.WriteAsync(result.ImageBytes, ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Avatar generation failed");
            await SendErrorResponseAsync(ex.Message, 502, ct);
        }
    }

    private async Task SendErrorResponseAsync(string message, int statusCode, CancellationToken ct)
    {
        await Send.ResponseAsync(new { statusCode, message }, statusCode, ct);
    }
}
