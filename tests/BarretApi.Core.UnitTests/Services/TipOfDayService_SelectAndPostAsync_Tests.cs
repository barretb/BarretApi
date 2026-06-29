using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class TipOfDayService_SelectAndPostAsync_Tests
{
	private readonly ITipOfDayRepository _tipRepository = Substitute.For<ITipOfDayRepository>();
	private readonly ISocialPlatformClient _platformClient = Substitute.For<ISocialPlatformClient>();
	private readonly TipOfDayService _sut;

	public TipOfDayService_SelectAndPostAsync_Tests()
	{
		_platformClient.PlatformName.Returns("bluesky");
		_platformClient.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration { Name = "bluesky", MaxCharacters = 500 });
		_platformClient.PostAsync(
				Arg.Any<string>(),
				Arg.Any<IReadOnlyList<UploadedImage>>(),
				Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = "bluesky",
				Success = true,
				PostId = "post-1",
				PublishedText = callInfo.ArgAt<string>(0)
			});

		var textShorteningService = Substitute.For<ITextShorteningService>();
		textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
			.Returns(callInfo => callInfo.ArgAt<string>(0));

		var hashtagService = Substitute.For<IHashtagService>();
		hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
			.Returns(callInfo => new HashtagProcessingResult
			{
				FinalText = callInfo.ArgAt<string>(0),
				AllHashtags = callInfo.ArgAt<IReadOnlyList<string>>(1)
			});

		var socialPostService = new SocialPostService(
			[_platformClient],
			textShorteningService,
			Substitute.For<ITextSplitterService>(),
			Substitute.For<IImageDownloadService>(),
			Substitute.For<IImageResizer>(),
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new TipOfDayService(
			_tipRepository,
			socialPostService,
			Options.Create(new TipOfDayOptions
			{
				RepostCooldownDays = 180,
				TableStorage = new TipOfDayTableStorageOptions
				{
					ConnectionString = "UseDevelopmentStorage=true"
				}
			}),
			Substitute.For<ILogger<TipOfDayService>>());
	}

	[Fact]
	public async Task PostsTipTextWithLeaderAndUrl_GivenEligibleTip()
	{
		var tip = CreateTip(moreInfoUrl: "https://example.com/more");
		_tipRepository.GetEligibleByCategoryAsync("dotnet", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([tip]);

		await _sut.SelectAndPostAsync(
			new TipOfDayPostCommand
			{
				Category = "dotnet",
				Platforms = ["bluesky"],
				Leader = "Tip of the day"
			});

		await _platformClient.Received(1).PostAsync(
			Arg.Is<string>(text => text == "Tip of the day\nUse primary constructors carefully.\nhttps://example.com/more"),
			Arg.Any<IReadOnlyList<UploadedImage>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MarksTipPosted_GivenAnyPlatformSucceeds()
	{
		var tip = CreateTip();
		_tipRepository.GetEligibleByCategoryAsync("dotnet", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([tip]);

		var result = await _sut.SelectAndPostAsync(new TipOfDayPostCommand { Category = "dotnet" });

		result.TipMarkedPosted.ShouldBeTrue();
		await _tipRepository.Received(1).MarkPostedAsync(
			tip.TipId,
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DoesNotMarkTipPosted_GivenAllPlatformsFail()
	{
		_platformClient.PostAsync(
				Arg.Any<string>(),
				Arg.Any<IReadOnlyList<UploadedImage>>(),
				Arg.Any<CancellationToken>())
			.Returns(new PlatformPostResult
			{
				Platform = "bluesky",
				Success = false,
				ErrorCode = "POST_FAILED",
				ErrorMessage = "Nope"
			});
		var tip = CreateTip();
		_tipRepository.GetEligibleByCategoryAsync("dotnet", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([tip]);

		var result = await _sut.SelectAndPostAsync(new TipOfDayPostCommand { Category = "dotnet" });

		result.TipMarkedPosted.ShouldBeFalse();
		await _tipRepository.DidNotReceive().MarkPostedAsync(
			Arg.Any<string>(),
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RequestsEligibleTipsUsingCooldownCutoff()
	{
		var before = DateTimeOffset.UtcNow.AddDays(-180).AddSeconds(-5);
		_tipRepository.GetEligibleByCategoryAsync("dotnet", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([CreateTip()]);

		await _sut.SelectAndPostAsync(new TipOfDayPostCommand { Category = "dotnet" });

		await _tipRepository.Received(1).GetEligibleByCategoryAsync(
			"dotnet",
			Arg.Is<DateTimeOffset>(cutoff => cutoff >= before && cutoff <= DateTimeOffset.UtcNow.AddDays(-180).AddSeconds(5)),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ThrowsInvalidOperation_GivenNoEligibleTips()
	{
		_tipRepository.GetEligibleByCategoryAsync("dotnet", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([]);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SelectAndPostAsync(new TipOfDayPostCommand { Category = "dotnet" }));

		ex.Message.ShouldContain("No eligible tips");
	}

	[Fact]
	public async Task AddsAllTips_GivenMultipleTips()
	{
		var result = await _sut.AddTipsAsync(
			"dotnet",
			[
				("Use nullable annotations.", null),
				("Prefer file-scoped namespaces.", "https://example.com/tip")
			]);

		result.Count.ShouldBe(2);
		result[0].Category.ShouldBe("dotnet");
		result[0].Tip.ShouldBe("Use nullable annotations.");
		result[0].MoreInfoUrl.ShouldBeNull();
		result[1].Tip.ShouldBe("Prefer file-scoped namespaces.");
		result[1].MoreInfoUrl.ShouldBe("https://example.com/tip");
		await _tipRepository.Received(2).AddAsync(
			Arg.Any<TipOfDayRecord>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ThrowsArgumentException_GivenNoTipsToAdd()
	{
		var ex = await Should.ThrowAsync<ArgumentException>(
			() => _sut.AddTipsAsync("dotnet", []));

		ex.Message.ShouldContain("At least one tip");
	}

	private static TipOfDayRecord CreateTip(string? moreInfoUrl = null)
	{
		return new TipOfDayRecord
		{
			TipId = "tip-1",
			Category = "dotnet",
			Tip = "Use primary constructors carefully.",
			MoreInfoUrl = moreInfoUrl,
			LastPostedDate = null,
			CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
		};
	}
}
