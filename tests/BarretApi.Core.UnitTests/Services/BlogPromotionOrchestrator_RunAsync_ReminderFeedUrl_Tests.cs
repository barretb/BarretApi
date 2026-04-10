using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class BlogPromotionOrchestrator_RunAsync_ReminderFeedUrl_Tests
{
    private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
    private readonly IBlogPostPromotionRepository _repository = Substitute.For<IBlogPostPromotionRepository>();
    private readonly ISocialPlatformClient _platformClient;
    private readonly BlogPromotionOrchestrator _sut;

    private const string ConfigFeedUrl = "https://example.com/feed.xml";
    private const string OtherFeedUrl = "https://other.example.com/feed.xml";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public BlogPromotionOrchestrator_RunAsync_ReminderFeedUrl_Tests()
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
            Substitute.For<ITextSplitterService>(),
            imageDownloadService,
            Substitute.For<IImageResizer>(),
            hashtagService,
            Substitute.For<ILogger<SocialPostService>>());

        var options = Options.Create(new BlogPromotionOptions
        {
            FeedUrl = ConfigFeedUrl,
            RecentDaysWindow = 7,
            EnableReminderPosts = true,
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
    public async Task SkipsReminderPost_GivenRecordFromDifferentFeedUrl()
    {
        var entry = CreateFeedEntry("entry-1", "My Post", "https://example.com/my-post");
        var recordFromOtherFeed = CreateReminderEligibleRecord(
            "other-entry", "Other Post", "https://other.example.com/other-post", OtherFeedUrl);

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(CreateAlreadyPostedRecord("entry-1", "My Post", "https://example.com/my-post", ConfigFeedUrl));
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([recordFromOtherFeed]);

        var result = await _sut.RunAsync();

        result.ReminderPostsAttempted.ShouldBe(0);
        await _platformClient.DidNotReceive().PostAsync(
            Arg.Is<string>(text => text.Contains("In case you missed it earlier")),
            Arg.Any<IReadOnlyList<UploadedImage>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostsReminder_GivenRecordFromMatchingFeedUrl()
    {
        var entry = CreateFeedEntry("entry-1", "My Post", "https://example.com/my-post");
        var recordFromSameFeed = CreateReminderEligibleRecord(
            "entry-1", "My Post", "https://example.com/my-post", ConfigFeedUrl);

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(recordFromSameFeed);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([recordFromSameFeed]);

        var result = await _sut.RunAsync();

        result.ReminderPostsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task SkipsReminderPost_GivenRecordWithNullFeedUrl()
    {
        var entry = CreateFeedEntry("entry-1", "My Post", "https://example.com/my-post");
        var recordWithoutFeedUrl = CreateReminderEligibleRecord(
            "old-entry", "Old Post", "https://example.com/old-post", feedUrl: null);

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(CreateAlreadyPostedRecord("entry-1", "My Post", "https://example.com/my-post", ConfigFeedUrl));
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([recordWithoutFeedUrl]);

        var result = await _sut.RunAsync();

        result.ReminderPostsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task FiltersRemindersCorrectly_GivenMixedFeedUrlRecords()
    {
        var entry = CreateFeedEntry("entry-1", "My Post", "https://example.com/my-post");
        var matchingRecord = CreateReminderEligibleRecord(
            "match-entry", "Match Post", "https://example.com/match", ConfigFeedUrl);
        var otherRecord = CreateReminderEligibleRecord(
            "other-entry", "Other Post", "https://other.example.com/other", OtherFeedUrl);

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(CreateAlreadyPostedRecord("entry-1", "My Post", "https://example.com/my-post", ConfigFeedUrl));
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([matchingRecord, otherRecord]);

        var result = await _sut.RunAsync();

        result.ReminderPostsAttempted.ShouldBe(1);
        result.ReminderPostsSucceeded.ShouldBe(1);
    }

    [Fact]
    public async Task MatchesFeedUrlCaseInsensitively_GivenDifferentCasing()
    {
        var entry = CreateFeedEntry("entry-1", "My Post", "https://example.com/my-post");
        var recordWithDifferentCasing = CreateReminderEligibleRecord(
            "entry-1", "My Post", "https://example.com/my-post", "HTTPS://EXAMPLE.COM/FEED.XML");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(recordWithDifferentCasing);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([recordWithDifferentCasing]);

        var result = await _sut.RunAsync();

        result.ReminderPostsAttempted.ShouldBe(1);
    }

    [Fact]
    public async Task StoresFeedUrlOnNewRecord_GivenInitialPost()
    {
        var entry = CreateFeedEntry("new-entry", "New Post", "https://example.com/new-post");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("new-entry", Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync();

        await _repository.Received(1).UpsertAsync(
            Arg.Is<BlogPostPromotionRecord>(r => r.FeedUrl == ConfigFeedUrl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoresFeedUrlOnNewRecord_GivenCustomFeedUrl()
    {
        var customUrl = "https://custom.example.com/rss.xml";
        var entry = CreateFeedEntry("new-entry", "New Post", "https://example.com/new-post");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("new-entry", Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync(feedUrl: customUrl);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<BlogPostPromotionRecord>(r => r.FeedUrl == customUrl),
            Arg.Any<CancellationToken>());
    }

    private static BlogFeedEntry CreateFeedEntry(string identity, string title, string url) =>
        new()
        {
            EntryIdentity = identity,
            CanonicalUrl = url,
            Title = title,
            PublishedAtUtc = Now.AddDays(-1),
            Tags = ["dotnet"]
        };

    private static BlogPostPromotionRecord CreateReminderEligibleRecord(
        string identity, string title, string url, string? feedUrl) =>
        new()
        {
            EntryIdentity = identity,
            CanonicalUrl = url,
            Title = title,
            PublishedAtUtc = Now.AddDays(-3),
            FeedUrl = feedUrl,
            InitialPostStatus = PostAttemptStatus.Succeeded,
            InitialPostSucceededAtUtc = Now.AddDays(-2),
            ReminderPostStatus = PostAttemptStatus.NotAttempted,
            LastProcessedAtUtc = Now.AddDays(-2)
        };

    private static BlogPostPromotionRecord CreateAlreadyPostedRecord(
        string identity, string title, string url, string feedUrl) =>
        new()
        {
            EntryIdentity = identity,
            CanonicalUrl = url,
            Title = title,
            PublishedAtUtc = Now.AddDays(-1),
            FeedUrl = feedUrl,
            InitialPostStatus = PostAttemptStatus.Succeeded,
            InitialPostSucceededAtUtc = Now.AddDays(-1),
            ReminderPostStatus = PostAttemptStatus.NotAttempted,
            LastProcessedAtUtc = Now.AddDays(-1)
        };
}
