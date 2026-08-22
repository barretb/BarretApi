using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class RssRandomJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IRssRandomPostService _service = Substitute.For<IRssRandomPostService>();

    private RssRandomJobHandler CreateSut(string configuredFeedUrl = "https://blog.example.com/index.xml")
        => new(
            _service,
            Options.Create(new BlogPromotionOptions { FeedUrl = configuredFeedUrl }),
            NullLogger<RssRandomJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson = null)
        => new("random-post", "rss-random", "run-1", Now, argumentsJson);

    private static RssRandomPostResult CreateResult(bool success)
        => new()
        {
            SelectedEntry = new BlogFeedEntry
            {
                EntryIdentity = "entry-1",
                Title = "A post",
                CanonicalUrl = "https://example.com/post",
                PublishedAtUtc = Now.AddDays(-3)
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
            ]
        };

    private static RssRandomPostResult CreatePartialResult()
        => new()
        {
            SelectedEntry = new BlogFeedEntry
            {
                EntryIdentity = "entry-1",
                Title = "A post",
                CanonicalUrl = "https://example.com/post",
                PublishedAtUtc = Now.AddDays(-3)
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = true },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "boom" }
            ]
        };

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("rss-random");
    }

    [Fact]
    public async Task FallsBackToTheConfiguredFeedUrl_GivenNoArguments()
    {
        _service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext());

        await _service.Received(1).SelectAndPostAsync(
            Arg.Is<RssRandomPostQuery>(q => q!.FeedUrl == "https://blog.example.com/index.xml"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesEveryConfiguredArgument()
    {
        _service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext(
            """{"feedUrl":"https://other.example/feed","platforms":["mastodon"],"excludeTags":["draft"],"maxAgeDays":90,"header":"ICYMI"}"""));

        await _service.Received(1).SelectAndPostAsync(
            Arg.Is<RssRandomPostQuery>(q =>
                q!.FeedUrl == "https://other.example/feed"
                && q.Platforms.Count == 1
                && q.Platforms[0] == "mastodon"
                && q.ExcludeTags.Count == 1
                && q.MaxAgeDays == 90
                && q.Header == "ICYMI"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_GivenNoFeedUrlIsAvailable()
    {
        var result = await CreateSut(configuredFeedUrl: string.Empty).ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("feedUrl");
        await _service.DidNotReceiveWithAnyArgs().SelectAndPostAsync(default!, default);
    }

    [Fact]
    public async Task Succeeds_GivenEveryPlatformPublished()
    {
        _service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("A post");
    }

    [Fact]
    public async Task Fails_GivenEveryPlatformFailed()
    {
        _service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: false));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("bluesky");
    }

    [Fact]
    public async Task Succeeds_GivenOnlySomePlatformsFailed()
    {
        _service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreatePartialResult());

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("bluesky");
        result.Summary!.ShouldContain("mastodon");
        result.Summary!.ShouldContain("boom");
    }
}
