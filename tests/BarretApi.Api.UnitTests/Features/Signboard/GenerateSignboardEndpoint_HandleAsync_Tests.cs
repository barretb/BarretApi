using BarretApi.Api.Features.Signboard;
using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Signboard;

public sealed class GenerateSignboardEndpoint_HandleAsync_Tests
{
	private readonly ISignboardGenerator _generator = Substitute.For<ISignboardGenerator>();
	private readonly SignboardPostService _postService;
	private readonly ILogger<GenerateSignboardEndpoint> _logger = Substitute.For<ILogger<GenerateSignboardEndpoint>>();

	private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

	public GenerateSignboardEndpoint_HandleAsync_Tests()
	{
		var socialPostService = new SocialPostService(
			Array.Empty<ISocialPlatformClient>(),
			Substitute.For<ITextShorteningService>(),
			Substitute.For<ITextSplitterService>(),
			Substitute.For<IImageDownloadService>(),
			Substitute.For<IImageResizer>(),
			Substitute.For<IHashtagService>(),
			Substitute.For<ILogger<SocialPostService>>());

		_postService = Substitute.For<SignboardPostService>(
			_generator,
			socialPostService,
			Substitute.For<ILogger<SignboardPostService>>());

		_generator.GenerateAsync(Arg.Any<SignboardGenerationCommand>(), Arg.Any<CancellationToken>())
			.Returns(FakePng);
	}

	[Fact]
	public async Task ReturnsPngBytes_GivenNoPlatforms()
	{
		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Seed = 42 };

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.ContentType.ShouldBe("image/png");
		ep.HttpContext.Response.Headers["X-Signboard-Seed"].ToString().ShouldBe("42");
		await _generator.Received(1).GenerateAsync(
			Arg.Is<SignboardGenerationCommand>(c =>
				c != null && c.Text == "HELLO" && c.Width == 1200 && c.Height == 900 && c.Seed == 42),
			Arg.Any<CancellationToken>());
		await _postService.DidNotReceiveWithAnyArgs().PostAsync(default!, default, default!, default, default!, default);
	}

	[Fact]
	public async Task PostsToPlatforms_GivenPlatformsSupplied()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new SignboardPostResult(1200, 900, 42, true,
			[
				new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" }
			]));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest
		{
			Text = "HELLO",
			Seed = 42,
			Platforms = ["bluesky"],
			Caption = "My caption",
			Hashtags = ["fun"]
		};

		await ep.HandleAsync(req, default);

		ep.Response.Seed.ShouldBe(42);
		ep.Response.Width.ShouldBe(1200);
		ep.Response.Height.ShouldBe(900);
		ep.Response.Results.Count.ShouldBe(1);
		ep.Response.Results[0].Platform.ShouldBe("bluesky");
		ep.Response.Results[0].Success.ShouldBeTrue();
		ep.HttpContext.Response.StatusCode.ShouldBe(200);
		await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default);
	}

	[Fact]
	public async Task Returns207_GivenPartialSuccess()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new SignboardPostResult(1200, 900, 42, true,
			[
				new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" },
				new PlatformPostResult
				{
					Platform = "mastodon",
					Success = false,
					ErrorMessage = "Rate limit exceeded",
					ErrorCode = "RATE_LIMITED"
				}
			]));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Platforms = ["bluesky", "mastodon"] };

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(207);
	}

	[Fact]
	public async Task MapsFailureDetails_GivenFailedPlatform()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new SignboardPostResult(1200, 900, 1, true,
			[
				new PlatformPostResult
				{
					Platform = "mastodon",
					Success = false,
					ErrorMessage = "Rate limit exceeded",
					ErrorCode = "RATE_LIMITED"
				}
			]));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Platforms = ["mastodon"] };

		await ep.HandleAsync(req, default);

		ep.Response.Results[0].Success.ShouldBeFalse();
		ep.Response.Results[0].Error.ShouldBe("Rate limit exceeded");
		ep.Response.Results[0].ErrorCode.ShouldBe("RATE_LIMITED");
		ep.HttpContext.Response.StatusCode.ShouldBe(502);
	}

	[Fact]
	public async Task Returns500_GivenPostServiceThrows()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("boom"));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Platforms = ["bluesky"] };

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(500);
	}
}
