using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class BlogPromotionOrchestrator_BuildReminderPostText_Tests
{
    private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
    private readonly IBlogPostPromotionRepository _repository = Substitute.For<IBlogPostPromotionRepository>();
    private readonly ISocialPlatformClient _platformClient;
    private readonly SocialPostService _socialPostService;
    private readonly BlogPromotionOrchestrator _sut;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public BlogPromotionOrchestrator_BuildReminderPostText_Tests()
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

        _socialPostService = new SocialPostService(
            [_platformClient],
            textShorteningService,
            imageDownloadService,
            Substitute.For<IImageResizer>(),
            hashtagService,
            Substitute.For<ILogger<SocialPostService>>());

        var options = Options.Create(new BlogPromotionOptions
        {
            FeedUrl = "https://example.com/feed.xml",
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
            _socialPostService,
            options,
            Substitute.For<ILogger<BlogPromotionOrchestrator>>());
    }

    [Fact]
    public async Task GeneratesCorrectReminderText_GivenEligibleRecord()
    {
        var entry = CreateFeedEntry("entry-1", "My Blog Post", "https://example.com/my-post");
        var trackedRecord = CreateReminderEligibleRecord("entry-1", "My Blog Post", "https://example.com/my-post");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(trackedRecord);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([trackedRecord]);

        await _sut.RunAsync();

        await _platformClient.Received(1).PostAsync(
            Arg.Is<string>(text => text == "In case you missed it earlier...\n\nMy Blog Post\nhttps://example.com/my-post"),
            Arg.Any<IReadOnlyList<UploadedImage>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UsesThreeAsciiPeriods_GivenReminderPost()
    {
        var entry = CreateFeedEntry("entry-1", "Test Title", "https://example.com/test");
        var trackedRecord = CreateReminderEligibleRecord("entry-1", "Test Title", "https://example.com/test");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("entry-1", Arg.Any<CancellationToken>())
            .Returns(trackedRecord);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([trackedRecord]);

        await _sut.RunAsync();

        await _platformClient.Received(1).PostAsync(
            Arg.Is<string>(text =>
                text.Contains("...") &&
                !text.Contains("\u2026")),
            Arg.Any<IReadOnlyList<UploadedImage>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotAlterInitialPostText_GivenNewEntry()
    {
        var entry = CreateFeedEntry("new-entry", "Fresh Post", "https://example.com/fresh");

        _feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([entry]);
        _repository.GetByEntryIdentityAsync("new-entry", Arg.Any<CancellationToken>())
            .Returns((BlogPostPromotionRecord?)null);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.RunAsync();

        await _platformClient.Received(1).PostAsync(
            Arg.Is<string>(text => text == "Fresh Post\nhttps://example.com/fresh"),
            Arg.Any<IReadOnlyList<UploadedImage>>(),
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

    private static BlogPostPromotionRecord CreateReminderEligibleRecord(string identity, string title, string url) =>
        new()
        {
            EntryIdentity = identity,
            CanonicalUrl = url,
            Title = title,
            PublishedAtUtc = Now.AddDays(-3),
            InitialPostStatus = PostAttemptStatus.Succeeded,
            InitialPostSucceededAtUtc = Now.AddDays(-2),
            ReminderPostStatus = PostAttemptStatus.NotAttempted,
            LastProcessedAtUtc = Now.AddDays(-2)
        };
}
