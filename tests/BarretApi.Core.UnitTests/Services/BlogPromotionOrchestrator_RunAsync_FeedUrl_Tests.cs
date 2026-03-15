using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class BlogPromotionOrchestrator_RunAsync_FeedUrl_Tests
{
    private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
    private readonly IBlogPostPromotionRepository _repository = Substitute.For<IBlogPostPromotionRepository>();
    private readonly ISocialPlatformClient _platformClient;
    private readonly BlogPromotionOrchestrator _sut;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public BlogPromotionOrchestrator_RunAsync_FeedUrl_Tests()
    {
        _platformClient = Substitute.For<ISocialPlatformClient>();
        _platformClient.PlatformName.Returns("testplatform");
        _platformClient.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(new PlatformConfiguration { Name = "testplatform", MaxCharacters = 500 });
        _platformClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new PlatformPostResult
            {
                Platform = "testplatform",
                Success = true,
                PostId = "test-post-id",
                PostUrl = "https://testplatform.com/post/1",
                PublishedText = callInfo.ArgAt<string>(0)
            });

        var textShorteningService = Substitute.For<ITextShorteningService>();
        textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
            .Returns(callInfo => callInfo.ArgAt<string>(0));

        var imageDownloadService = Substitute.For<IImageDownloadService>();

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
            imageDownloadService,
            hashtagService,
            Substitute.For<ILogger<SocialPostService>>());

        var options = Options.Create(new BlogPromotionOptions
        {
            FeedUrl = "https://example.com/default-feed.xml",
            RecentDaysWindow = 7,
            EnableReminderPosts = false,
            ReminderDelayHours = 24,
            TableStorage = new BlogPromotionTableStorageOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                TableName = "promotions",
                PartitionKey = "test"
            }
        });

        _sut = new BlogPromotionOrchestrator(
            _feedReader,
            _repository,
            socialPostService,
            options,
            Substitute.For<ILogger<BlogPromotionOrchestrator>>());
    }

    [Fact]
    public async Task UsesConfigFeedUrl_GivenNullFeedUrl()
    {
        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync(feedUrl: null);

        await _feedReader.Received(1).ReadEntriesAsync(
            "https://example.com/default-feed.xml",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UsesConfigFeedUrl_GivenEmptyFeedUrl()
    {
        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync(feedUrl: "");

        await _feedReader.Received(1).ReadEntriesAsync(
            "https://example.com/default-feed.xml",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UsesProvidedFeedUrl_GivenCustomFeedUrl()
    {
        var customUrl = "https://other.example.com/rss.xml";
        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync(feedUrl: customUrl);

        await _feedReader.Received(1).ReadEntriesAsync(
            customUrl,
            Arg.Any<CancellationToken>());
    }
}
