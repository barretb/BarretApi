using BarretApi.Api.Features.SocialPost;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.SocialPost;

public sealed class RssRandomPostValidator_FeedUrl_Tests
{
    private readonly RssRandomPostValidator _validator = new();

    [Fact]
    public async Task ReturnsError_GivenNullFeedUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = null };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenEmptyFeedUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenWhitespaceFeedUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "   " };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenRelativeUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "/feed.xml" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenFtpScheme()
    {
        var request = new RssRandomPostRequest { FeedUrl = "ftp://example.com/feed.xml" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenFileScheme()
    {
        var request = new RssRandomPostRequest { FeedUrl = "file:///etc/passwd" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsNoError_GivenValidHttpUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "http://example.com/feed.xml" };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsNoError_GivenValidHttpsUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "https://example.com/blog/feed.xml" };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "FeedUrl");
    }

    [Fact]
    public async Task ReturnsError_GivenMalformedUrl()
    {
        var request = new RssRandomPostRequest { FeedUrl = "not-a-url" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "FeedUrl");
    }
}

public sealed class RssRandomPostValidator_Platforms_Tests
{
    private readonly RssRandomPostValidator _validator = new();
    private const string ValidFeedUrl = "https://example.com/feed.xml";

    [Fact]
    public async Task ReturnsNoError_GivenNullPlatforms()
    {
        var request = new RssRandomPostRequest { FeedUrl = ValidFeedUrl, Platforms = null };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Platforms"));
    }

    [Fact]
    public async Task ReturnsNoError_GivenEmptyPlatforms()
    {
        var request = new RssRandomPostRequest { FeedUrl = ValidFeedUrl, Platforms = [] };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Platforms"));
    }

    [Theory]
    [InlineData("bluesky")]
    [InlineData("mastodon")]
    [InlineData("linkedin")]
    [InlineData("Bluesky")]
    [InlineData("MASTODON")]
    [InlineData("LinkedIn")]
    public async Task ReturnsNoError_GivenSupportedPlatform(string platform)
    {
        var request = new RssRandomPostRequest { FeedUrl = ValidFeedUrl, Platforms = [platform] };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Platforms"));
    }

    [Theory]
    [InlineData("twitter")]
    [InlineData("facebook")]
    [InlineData("instagram")]
    [InlineData("unknown")]
    public async Task ReturnsError_GivenUnsupportedPlatform(string platform)
    {
        var request = new RssRandomPostRequest { FeedUrl = ValidFeedUrl, Platforms = [platform] };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Platforms"));
    }

    [Fact]
    public async Task ReturnsNoError_GivenMultipleSupportedPlatforms()
    {
        var request = new RssRandomPostRequest
        {
            FeedUrl = ValidFeedUrl,
            Platforms = ["bluesky", "mastodon", "linkedin"]
        };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Platforms"));
    }
}

public sealed class RssRandomPostValidator_MaxAgeDays_Tests
{
    private readonly RssRandomPostValidator _validator = new();
    private const string ValidFeedUrl = "https://example.com/feed.xml";

    [Fact]
    public async Task ReturnsNoError_GivenNullMaxAgeDays()
    {
        var request = new RssRandomPostRequest
        {
            FeedUrl = ValidFeedUrl,
            MaxAgeDays = null
        };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "MaxAgeDays");
    }

    [Fact]
    public async Task ReturnsNoError_GivenPositiveMaxAgeDays()
    {
        var request = new RssRandomPostRequest
        {
            FeedUrl = ValidFeedUrl,
            MaxAgeDays = 7
        };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "MaxAgeDays");
    }

    [Fact]
    public async Task ReturnsError_GivenZeroMaxAgeDays()
    {
        var request = new RssRandomPostRequest
        {
            FeedUrl = ValidFeedUrl,
            MaxAgeDays = 0
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "MaxAgeDays");
    }

    [Fact]
    public async Task ReturnsError_GivenNegativeMaxAgeDays()
    {
        var request = new RssRandomPostRequest
        {
            FeedUrl = ValidFeedUrl,
            MaxAgeDays = -5
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "MaxAgeDays");
    }
}
