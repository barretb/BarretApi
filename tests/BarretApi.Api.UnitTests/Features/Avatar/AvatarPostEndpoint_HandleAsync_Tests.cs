using BarretApi.Api.Features.Avatar;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Avatar;

public sealed class AvatarPostEndpoint_HandleAsync_Tests
{
	private readonly AvatarPostService _service;
	private readonly ILogger<AvatarPostEndpoint> _logger = Substitute.For<ILogger<AvatarPostEndpoint>>();

	public AvatarPostEndpoint_HandleAsync_Tests()
	{
		var socialPostService = new SocialPostService(
			Array.Empty<ISocialPlatformClient>(),
			Substitute.For<ITextShorteningService>(),
			Substitute.For<IImageDownloadService>(),
			Substitute.For<IImageResizer>(),
			Substitute.For<IHashtagService>(),
			Substitute.For<ILogger<SocialPostService>>());

		_service = Substitute.For<AvatarPostService>(
			Substitute.For<IDiceBearAvatarClient>(),
			socialPostService,
			Substitute.For<ILogger<AvatarPostService>>());
	}

	private static AvatarPostResult CreateSuccessResult()
	{
		return new AvatarPostResult
		{
			Style = "pixel-art",
			Seed = "test-seed",
			Format = "png",
			ImageAttached = true,
			PlatformResults =
			[
				new PlatformPostResult
				{
					Platform = "bluesky",
					Success = true,
					PostId = "post-1",
					PostUrl = "https://bsky.app/post/1"
				}
			]
		};
	}

	[Fact]
	public async Task ResponseContainsAvatarMetadata_GivenSuccessfulPost()
	{
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateSuccessResult());

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest { Style = "pixel-art", Seed = "test-seed" };

		await ep.HandleAsync(req, default);

		ep.Response.Style.ShouldBe("pixel-art");
		ep.Response.Seed.ShouldBe("test-seed");
		ep.Response.Format.ShouldBe("png");
		ep.Response.ImageAttached.ShouldBeTrue();
	}

	[Fact]
	public async Task PassesRequestParametersToService_GivenFullRequest()
	{
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateSuccessResult());

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest
		{
			Style = "bottts",
			Seed = "my-seed",
			Text = "Check this out!",
			AltText = "A cool avatar",
			Hashtags = ["avatar"],
			Platforms = ["bluesky", "mastodon"]
		};

		await ep.HandleAsync(req, default);

		await _service.Received(1).PostAsync(
			"bottts",
			"my-seed",
			"Check this out!",
			"A cool avatar",
			Arg.Is<IReadOnlyList<string>>(h => h.Contains("avatar")),
			Arg.Is<IReadOnlyList<string>>(p => p.Contains("bluesky") && p.Contains("mastodon")),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DefaultsToEmptyStrings_GivenMinimalRequest()
	{
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateSuccessResult());

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		await _service.Received(1).PostAsync(
			null,
			null,
			string.Empty,
			string.Empty,
			Arg.Is<IReadOnlyList<string>>(h => h.Count == 0),
			Arg.Is<IReadOnlyList<string>>(p => p.Count == 0),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Returns200_GivenAllPlatformsSucceed()
	{
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateSuccessResult());

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(200);
	}

	[Fact]
	public async Task Returns207_GivenPartialSuccess()
	{
		var result = new AvatarPostResult
		{
			Style = "pixel-art",
			Seed = "test",
			Format = "png",
			ImageAttached = true,
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" },
				new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "Auth failed", ErrorCode = "AUTH_FAILED" }
			]
		};
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(result);

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(207);
	}

	[Fact]
	public async Task Returns502_GivenAllPlatformsFail()
	{
		var result = new AvatarPostResult
		{
			Style = "pixel-art",
			Seed = "test",
			Format = "png",
			ImageAttached = true,
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" },
				new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" }
			]
		};
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(result);

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(502);
	}

	[Fact]
	public async Task Returns502_GivenAvatarGenerationFails()
	{
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("The avatar generation service is temporarily unavailable."));

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(502);
	}

	[Fact]
	public async Task ResponseContainsPlatformResults_GivenMultiplePlatforms()
	{
		var result = new AvatarPostResult
		{
			Style = "adventurer",
			Seed = "multi-seed",
			Format = "png",
			ImageAttached = true,
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1", PostUrl = "https://bsky.app/post/1", PublishedText = "Hello!" },
				new PlatformPostResult { Platform = "mastodon", Success = true, PostId = "p2", PostUrl = "https://mastodon.social/post/2", PublishedText = "Hello!" }
			]
		};
		_service.PostAsync(
				Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(),
				Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(result);

		var ep = Factory.Create<AvatarPostEndpoint>(_service, _logger);
		var req = new AvatarPostRequest();

		await ep.HandleAsync(req, default);

		ep.Response.Results.Count.ShouldBe(2);
		ep.Response.Results.ShouldContain(r => r.Platform == "bluesky" && r.Success);
		ep.Response.Results.ShouldContain(r => r.Platform == "mastodon" && r.Success);
	}
}
