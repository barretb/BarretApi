using BarretApi.Api.Features.Avatar;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Avatar;

public sealed class GenerateAvatarEndpoint_HandleAsync_Tests
{
    private readonly IDiceBearAvatarClient _avatarClient = Substitute.For<IDiceBearAvatarClient>();
    private readonly ILogger<GenerateAvatarEndpoint> _logger = Substitute.For<ILogger<GenerateAvatarEndpoint>>();

    [Fact]
    public async Task ReturnsImageBytes_GivenSuccessfulAvatarGeneration()
    {
        var expectedBytes = new byte[] { 0x3C, 0x73, 0x76, 0x67 };
        var avatarResult = new AvatarResult
        {
            ImageBytes = expectedBytes,
            ContentType = "image/svg+xml",
            Style = "pixel-art",
            Seed = "test-seed",
            Format = "svg"
        };
        _avatarClient
            .GetAvatarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(avatarResult);

        var ep = Factory.Create<GenerateAvatarEndpoint>(
            _avatarClient, _logger);
        var req = new GenerateAvatarRequest
        {
            Style = "pixel-art",
            Seed = "test-seed"
        };

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.ContentType.ShouldBe("image/svg+xml");
    }

    [Fact]
    public async Task PassesRequestParametersToClient_GivenStyleAndSeed()
    {
        var avatarResult = new AvatarResult
        {
            ImageBytes = [0x01],
            ContentType = "image/svg+xml",
            Style = "bottts",
            Seed = "my-seed",
            Format = "svg"
        };
        _avatarClient
            .GetAvatarAsync("bottts", "png", "my-seed", Arg.Any<CancellationToken>())
            .Returns(avatarResult);

        var ep = Factory.Create<GenerateAvatarEndpoint>(
            _avatarClient, _logger);
        var req = new GenerateAvatarRequest
        {
            Style = "bottts",
            Format = "png",
            Seed = "my-seed"
        };

        await ep.HandleAsync(req, default);

        await _avatarClient.Received(1).GetAvatarAsync(
            "bottts", "png", "my-seed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns502_GivenUpstreamServiceFailure()
    {
        _avatarClient
            .GetAvatarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The avatar generation service is temporarily unavailable."));

        var ep = Factory.Create<GenerateAvatarEndpoint>(
            _avatarClient, _logger);
        var req = new GenerateAvatarRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(502);
    }
}
