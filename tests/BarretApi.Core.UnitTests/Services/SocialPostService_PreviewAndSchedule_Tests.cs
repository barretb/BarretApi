using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class SocialPostService_PreviewAndSchedule_Tests
{
	[Theory]
	[InlineData(true, "A long post with several words and paragraphs\n\nSecond paragraph.")]
	[InlineData(true, "👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻👩‍💻")]
	[InlineData(false, "A long post with several words")]
	[InlineData(true, "short")]
	public async Task MatchesPublishedText_GivenPreview(bool autoThread, string text)
	{
		var client = Client();
		IReadOnlyList<ThreadSegmentPost>? postedSegments = null;
		string? postedText = null;
		client.PostThreadAsync(Arg.Any<IReadOnlyList<ThreadSegmentPost>>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				postedSegments = call.Arg<IReadOnlyList<ThreadSegmentPost>>();
				return postedSegments!.Select(segment => new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = segment.Text }).ToArray();
			});
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(call => { postedText = call.Arg<string>(); return new PlatformPostResult { Platform = "bluesky", Success = true }; });
		var service = Service(client);
		var post = new SocialPost { Text = text, AutoThread = autoThread };

		var preview = (await service.PreviewAsync(post)).Single();
		await client.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
		await client.DidNotReceiveWithAnyArgs().PostThreadAsync(default!, default);
		await client.DidNotReceiveWithAnyArgs().UploadImageAsync(default!, default);
		await service.PostAsync(post);

		if (preview.ThreadResults is not null)
		{
			postedSegments.ShouldNotBeNull();
			preview.ThreadResults.Select(segment => segment.PublishedText).ShouldBe(postedSegments.Select(segment => segment.Text));
		}
		else
		{
			preview.PublishedText.ShouldBe(postedText);
		}
	}

	[Fact]
	public async Task PreservesThreadingAndFreezesTargets_GivenScheduledPost()
	{
		var repository = Substitute.For<IScheduledSocialPostRepository>();
		var service = Service(Client(), repository);

		await service.ScheduleAsync(new SocialPost { Text = "scheduled thread", AutoThread = true, ScheduledForUtc = DateTimeOffset.UtcNow.AddHours(1) });

		await repository.Received(1).SaveScheduledAsync(Arg.Is<ScheduledSocialPostRecord>(record =>
			record != null && record.AutoThread && record.DeliveryTrackingEnabled && record.TargetPlatforms.SequenceEqual(new[] { "bluesky" })), Arg.Any<CancellationToken>());
	}

	private static ISocialPlatformClient Client()
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns("bluesky");
		client.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new PlatformConfiguration { Name = "bluesky", MaxCharacters = 10 });
		return client;
	}

	private static SocialPostService Service(ISocialPlatformClient client, IScheduledSocialPostRepository? repository = null)
	{
		return new SocialPostService([client], new TextShorteningService(), new TextSplitterService(),
			Substitute.For<IImageDownloadService>(), Substitute.For<IImageResizer>(), new HashtagService(),
			NullLogger<SocialPostService>.Instance, repository);
	}
}
