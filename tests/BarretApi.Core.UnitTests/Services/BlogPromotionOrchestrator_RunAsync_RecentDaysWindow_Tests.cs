using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class BlogPromotionOrchestrator_RunAsync_RecentDaysWindow_Tests
{
    private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
    private readonly IBlogPostPromotionRepository _repository = Substitute.For<IBlogPostPromotionRepository>();
    private readonly BlogPromotionOrchestrator _sut;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public BlogPromotionOrchestrator_RunAsync_RecentDaysWindow_Tests()
    {
        var platformClient = Substitute.For<ISocialPlatformClient>();
        platformClient.PlatformName.Returns("testplatform");
        platformClient.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(new PlatformConfiguration { Name = "testplatform", MaxCharacters = 500 });
        platformClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
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
            [platformClient],
            textShorteningService,
            imageDownloadService,
            Substitute.For<IImageResizer>(),
            hashtagService,
            Substitute.For<ILogger<SocialPostService>>());

        var options = Options.Create(new BlogPromotionOptions
        {
            FeedUrl = "https://example.com/feed.xml",
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
    public async Task UsesConfigRecentDaysWindow_GivenNullOverride()
    {
        var recentEntry = CreateFeedEntry("recent", Now.AddDays(-3));
        var oldEntry = CreateFeedEntry("old", Now.AddDays(-10));

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([recentEntry, oldEntry]);
        _repository.GetByEntryIdentityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.RunAsync(recentDaysWindow: null);

        result.NewPostsAttempted.ShouldBe(1);
        result.EntriesSkippedOutsideWindow.ShouldBe(1);
    }

    [Fact]
    public async Task UsesOverrideRecentDaysWindow_GivenLargerValue()
    {
        var recentEntry = CreateFeedEntry("recent", Now.AddDays(-3));
        var oldEntry = CreateFeedEntry("old", Now.AddDays(-10));

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([recentEntry, oldEntry]);
        _repository.GetByEntryIdentityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.RunAsync(recentDaysWindow: 30);

        result.NewPostsAttempted.ShouldBe(2);
        result.EntriesSkippedOutsideWindow.ShouldBe(0);
    }

    [Fact]
    public async Task UsesOverrideRecentDaysWindow_GivenSmallerValue()
    {
        var recentEntry = CreateFeedEntry("recent", Now.AddDays(-1));
        var mediumEntry = CreateFeedEntry("medium", Now.AddDays(-3));

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([recentEntry, mediumEntry]);
        _repository.GetByEntryIdentityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.RunAsync(recentDaysWindow: 2);

        result.NewPostsAttempted.ShouldBe(1);
        result.EntriesSkippedOutsideWindow.ShouldBe(1);
    }

    private static BlogFeedEntry CreateFeedEntry(string identity, DateTimeOffset publishedAt) =>
        new()
        {
            EntryIdentity = identity,
            CanonicalUrl = $"https://example.com/{identity}",
            Title = $"Post {identity}",
            PublishedAtUtc = publishedAt,
            Tags = ["dotnet"]
        };
}
