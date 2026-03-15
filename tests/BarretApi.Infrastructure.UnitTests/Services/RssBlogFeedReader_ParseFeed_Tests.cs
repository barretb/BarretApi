using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BarretApi.Core.Configuration;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class RssBlogFeedReader_ParseFeed_Tests
{
    private const string BarretNamespace = "https://barretblake.dev/ns/";

    private readonly RssBlogFeedReader _sut;
    private readonly HttpClient _httpClient;

    public RssBlogFeedReader_ParseFeed_Tests()
    {
        _httpClient = new HttpClient(new FakeFeedHandler());
        var options = Options.Create(new BlogPromotionOptions { FeedUrl = "https://example.com/feed.xml" });
        _sut = new RssBlogFeedReader(_httpClient, options, Substitute.For<ILogger<RssBlogFeedReader>>());
    }

    #region T010: Summary fallback from Atom <content>

    [Fact]
    public async Task ParsesFeed_ExtractsSummaryFromAtomContent_GivenNoSummaryElement()
    {
        var feed = BuildAtomFeed(summaryText: null, contentText: "This is the content body");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries.Count.ShouldBe(1);
        entries[0].Summary.ShouldBe("This is the content body");
    }

    [Fact]
    public async Task ParsesFeed_PrefersSummaryOverContent_GivenBothPresent()
    {
        var feed = BuildAtomFeed(summaryText: "Short summary", contentText: "Full content body");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Summary.ShouldBe("Short summary");
    }

    [Fact]
    public async Task ParsesFeed_ReturnsNullSummary_GivenNoSummaryAndNoContent()
    {
        var feed = BuildAtomFeed(summaryText: null, contentText: null);
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Summary.ShouldBeNull();
    }

    #endregion

    #region T010: HTML stripping

    [Fact]
    public async Task ParsesFeed_StripsHtmlFromSummary_GivenHtmlContent()
    {
        var htmlSummary = "<p>This is <strong>bold</strong> and <em>italic</em> text.</p>";
        var feed = BuildRssFeed(description: htmlSummary);
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Summary.ShouldBe("This is bold and italic text.");
    }

    [Fact]
    public async Task ParsesFeed_StripsScriptAndStyleTags_GivenComplexHtml()
    {
        var htmlSummary = "<p>Visible text</p><script>alert('xss')</script><style>.hidden{}</style><p>More text</p>";
        var feed = BuildRssFeed(description: htmlSummary);
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        var summary = entries[0].Summary.ShouldNotBeNull();
        summary.ShouldNotContain("alert");
        summary.ShouldNotContain(".hidden");
        summary.ShouldContain("Visible text");
        summary.ShouldContain("More text");
    }

    [Fact]
    public async Task ParsesFeed_ReturnsNullSummary_GivenWhitespaceOnlyHtml()
    {
        var feed = BuildRssFeed(description: "<p>  </p>");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Summary.ShouldBeNull();
    }

    [Fact]
    public async Task ParsesFeed_ReturnsPlainText_GivenPlainTextSummary()
    {
        var feed = BuildRssFeed(description: "Just plain text, no HTML");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Summary.ShouldBe("Just plain text, no HTML");
    }

    #endregion

    #region T010: Hero image from enclosure

    [Fact]
    public async Task ParsesFeed_ExtractsHeroImageFromEnclosure_GivenImageEnclosure()
    {
        var feed = BuildRssFeedWithEnclosure("https://example.com/hero.jpg", "image/jpeg");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].HeroImageUrl.ShouldBe("https://example.com/hero.jpg");
    }

    [Fact]
    public async Task ParsesFeed_IgnoresNonImageEnclosure_GivenAudioEnclosure()
    {
        var feed = BuildRssFeedWithEnclosure("https://example.com/podcast.mp3", "audio/mpeg");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].HeroImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ParsesFeed_ExtractsHeroImageFromMediaThumbnail_GivenNoEnclosure()
    {
        var feed = BuildRssFeedWithMediaElement("thumbnail", "https://example.com/thumb.png");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].HeroImageUrl.ShouldBe("https://example.com/thumb.png");
    }

    [Fact]
    public async Task ParsesFeed_ExtractsHeroImageFromMediaContent_GivenNoThumbnail()
    {
        var feed = BuildRssFeedWithMediaElement("content", "https://example.com/media.jpg");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].HeroImageUrl.ShouldBe("https://example.com/media.jpg");
    }

    #endregion

    #region T012: Custom tags take precedence over standard categories

    [Fact]
    public async Task ParsesFeed_UsesCustomTags_GivenBothCustomTagsAndCategories()
    {
        var feed = BuildFeedWithCustomTagsAndCategories(
            customTags: ["dotnet", "aspire"],
            categories: ["programming", "cloud"]);
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Tags.ShouldBe(new[] { "dotnet", "aspire" });
    }

    #endregion

    #region T013: Custom hero image takes precedence over enclosure and media

    [Fact]
    public async Task ParsesFeed_UsesCustomHeroImage_GivenBothCustomAndEnclosure()
    {
        var feed = BuildFeedWithCustomHeroAndEnclosure(
            customHero: "https://example.com/custom-hero.jpg",
            enclosureUrl: "https://example.com/enclosure.jpg");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].HeroImageUrl.ShouldBe("https://example.com/custom-hero.jpg");
    }

    #endregion

    #region T014: Mixed extension feed

    [Fact]
    public async Task ParsesFeed_HandlesMixedEntries_GivenCustomAndStandardEntries()
    {
        var feed = BuildMixedExtensionFeed();
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries.Count.ShouldBe(2);

        var customEntry = entries.First(e => e.EntryIdentity == "custom-entry");
        customEntry.Tags.ShouldBe(new[] { "dotnet" });
        customEntry.HeroImageUrl.ShouldBe("https://example.com/custom-hero.jpg");

        var standardEntry = entries.First(e => e.EntryIdentity == "standard-entry");
        standardEntry.Tags.ShouldBe(new[] { "programming" });
        standardEntry.HeroImageUrl.ShouldBe("https://example.com/enclosure.jpg");
    }

    #endregion

    #region T016: Category-to-tag fallback

    [Fact]
    public async Task ParsesFeed_FallsBackToCategories_GivenNoCustomTags()
    {
        var feed = BuildRssFeedWithCategories(["tech", "blog"]);
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Tags.ShouldBe(new[] { "tech", "blog" });
    }

    [Fact]
    public async Task ParsesFeed_ReturnsEmptyTags_GivenNoCustomTagsAndNoCategories()
    {
        var feed = BuildRssFeed(description: "A post");
        SetFeed(feed);

        var entries = await _sut.ReadEntriesAsync("https://example.com/feed.xml");

        entries[0].Tags.ShouldBeEmpty();
    }

    #endregion

    #region Feed builders

    private SyndicationFeed? _currentFeed;

    private void SetFeed(SyndicationFeed feed)
    {
        _currentFeed = feed;
        var handler = (FakeFeedHandler)GetHandler();
        handler.FeedFactory = () => SerializeFeed(_currentFeed!);
    }

    private HttpMessageHandler GetHandler()
    {
        var field = typeof(HttpMessageInvoker).GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (HttpMessageHandler)field!.GetValue(_httpClient)!;
    }

    private static SyndicationFeed BuildAtomFeed(string? summaryText, string? contentText)
    {
        var item = new SyndicationItem
        {
            Id = "atom-entry-1",
            Title = new TextSyndicationContent("Test Atom Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        if (summaryText is not null)
        {
            item.Summary = new TextSyndicationContent(summaryText);
        }

        if (contentText is not null)
        {
            item.Content = new TextSyndicationContent(contentText);
        }

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildRssFeed(string? description)
    {
        var item = new SyndicationItem
        {
            Id = "rss-entry-1",
            Title = new TextSyndicationContent("Test RSS Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        if (description is not null)
        {
            item.Summary = new TextSyndicationContent(description);
        }

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildRssFeedWithEnclosure(string url, string mediaType)
    {
        var item = new SyndicationItem
        {
            Id = "enclosure-entry-1",
            Title = new TextSyndicationContent("Enclosure Test Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));
        item.Links.Add(new SyndicationLink(new Uri(url), "enclosure", "Enclosure", mediaType, 0));

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildRssFeedWithMediaElement(string elementName, string url)
    {
        const string mediaNamespace = "http://search.yahoo.com/mrss/";
        var item = new SyndicationItem
        {
            Id = "media-entry-1",
            Title = new TextSyndicationContent("Media Test Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        var mediaElement = new XElement(XName.Get(elementName, mediaNamespace), new XAttribute("url", url));
        item.ElementExtensions.Add(mediaElement);

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildRssFeedWithCategories(string[] categories)
    {
        var item = new SyndicationItem
        {
            Id = "category-entry-1",
            Title = new TextSyndicationContent("Category Test Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1),
            Summary = new TextSyndicationContent("A test post")
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        foreach (var cat in categories)
        {
            item.Categories.Add(new SyndicationCategory(cat));
        }

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildFeedWithCustomTagsAndCategories(
        string[] customTags, string[] categories)
    {
        var item = new SyndicationItem
        {
            Id = "mixed-tags-entry-1",
            Title = new TextSyndicationContent("Mixed Tags Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        var tagsElement = new XElement(XName.Get("tags", BarretNamespace));
        foreach (var tag in customTags)
        {
            tagsElement.Add(new XElement(XName.Get("tag", BarretNamespace), tag));
        }

        item.ElementExtensions.Add(tagsElement);

        foreach (var cat in categories)
        {
            item.Categories.Add(new SyndicationCategory(cat));
        }

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildFeedWithCustomHeroAndEnclosure(
        string customHero, string enclosureUrl)
    {
        var item = new SyndicationItem
        {
            Id = "hero-fallback-entry-1",
            Title = new TextSyndicationContent("Hero Fallback Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/post-1")));

        var heroElement = new XElement(XName.Get("hero", BarretNamespace), customHero);
        item.ElementExtensions.Add(heroElement);
        item.Links.Add(new SyndicationLink(new Uri(enclosureUrl), "enclosure", "Image", "image/jpeg", 0));

        return new SyndicationFeed([item]);
    }

    private static SyndicationFeed BuildMixedExtensionFeed()
    {
        var customItem = new SyndicationItem
        {
            Id = "custom-entry",
            Title = new TextSyndicationContent("Custom Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        customItem.Links.Add(new SyndicationLink(new Uri("https://example.com/custom")));

        var tagsElement = new XElement(XName.Get("tags", BarretNamespace));
        tagsElement.Add(new XElement(XName.Get("tag", BarretNamespace), "dotnet"));
        customItem.ElementExtensions.Add(tagsElement);

        var heroElement = new XElement(XName.Get("hero", BarretNamespace), "https://example.com/custom-hero.jpg");
        customItem.ElementExtensions.Add(heroElement);

        var standardItem = new SyndicationItem
        {
            Id = "standard-entry",
            Title = new TextSyndicationContent("Standard Entry"),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-2)
        };
        standardItem.Links.Add(new SyndicationLink(new Uri("https://example.com/standard")));
        standardItem.Categories.Add(new SyndicationCategory("programming"));
        standardItem.Links.Add(new SyndicationLink(new Uri("https://example.com/enclosure.jpg"), "enclosure", "Image", "image/jpeg", 0));

        return new SyndicationFeed([customItem, standardItem]);
    }

    private static byte[] SerializeFeed(SyndicationFeed feed)
    {
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Encoding.UTF8 }))
        {
            var formatter = new Atom10FeedFormatter(feed);
            formatter.WriteTo(writer);
        }

        return ms.ToArray();
    }

    #endregion

    private sealed class FakeFeedHandler : HttpMessageHandler
    {
        public Func<byte[]>? FeedFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = FeedFactory?.Invoke() ?? throw new InvalidOperationException("No feed configured");
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/atom+xml");
            return Task.FromResult(response);
        }
    }
}
