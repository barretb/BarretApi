using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AngleSharp;
using AngleSharp.Html.Parser;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed partial class RssBlogFeedReader(
    HttpClient httpClient,
    IOptions<BlogPromotionOptions> blogPromotionOptions,
    ILogger<RssBlogFeedReader> logger) : IBlogFeedReader
{
    private const string BarretNamespace = "https://barretblake.dev/ns/";
    private const string MediaNamespace = "http://search.yahoo.com/mrss/";

    [GeneratedRegex(@"(?<=\s|^)#(\w+)")]
    private static partial Regex InlineHashtagPattern();

    private readonly HttpClient _httpClient = httpClient;
    private readonly BlogPromotionOptions _options = blogPromotionOptions.Value;
    private readonly ILogger<RssBlogFeedReader> _logger = logger;

    public async Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.FeedUrl);
        return await ReadEntriesAsync(_options.FeedUrl, cancellationToken);
    }

    public async Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(
        string feedUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);

        var uri = new Uri(feedUrl);
        using var stream = await _httpClient.GetStreamAsync(uri, cancellationToken);
        return ParseFeed(stream);
    }

    private IReadOnlyList<BlogFeedEntry> ParseFeed(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var feed = SyndicationFeed.Load(reader)
            ?? throw new InvalidOperationException("RSS feed could not be parsed.");

        var entries = new List<BlogFeedEntry>();
        foreach (var item in feed.Items)
        {
            var canonicalUrl = item.Links.FirstOrDefault()?.Uri?.ToString();
            if (string.IsNullOrWhiteSpace(canonicalUrl))
            {
                continue;
            }

            var guid = string.IsNullOrWhiteSpace(item.Id) ? null : item.Id;
            var entryIdentity = guid ?? canonicalUrl;
            if (string.IsNullOrWhiteSpace(entryIdentity))
            {
                continue;
            }

            var publishedAt = item.PublishDate == DateTimeOffset.MinValue
                ? (item.LastUpdatedTime == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : item.LastUpdatedTime.ToUniversalTime())
                : item.PublishDate.ToUniversalTime();

            entries.Add(new BlogFeedEntry
            {
                EntryIdentity = entryIdentity,
                Guid = guid,
                CanonicalUrl = canonicalUrl,
                Title = item.Title?.Text ?? canonicalUrl,
                PublishedAtUtc = publishedAt,
                Summary = ReadSummary(item),
                HeroImageUrl = ReadHeroImageUrl(item),
                Tags = ReadTags(item)
            });
        }

        _logger.LogInformation("Read {EntryCount} entries from RSS feed", entries.Count);
        return entries;
    }

    private static string? ReadHeroImageUrl(SyndicationItem item)
    {
        var heroElements = item.ElementExtensions.ReadElementExtensions<XElement>("hero", BarretNamespace);
        var customHero = heroElements
            .Select(element => element.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(customHero))
        {
            return customHero;
        }

        var enclosureImage = item.Links
            .Where(link => string.Equals(link.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase))
            .Where(link => link.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            .Select(link => link.Uri?.ToString())
            .FirstOrDefault(url => IsAbsoluteHttpUrl(url));

        if (enclosureImage is not null)
        {
            return enclosureImage;
        }

        var mediaRssImage = ReadMediaRssImageUrl(item);
        if (mediaRssImage is not null)
        {
            return mediaRssImage;
        }

        var itemImageUrl = ReadItemImageUrl(item);
        if (itemImageUrl is not null)
        {
            return itemImageUrl;
        }

        return ReadContentImageUrl(item);
    }

    private static string? ReadItemImageUrl(SyndicationItem item)
    {
        var imageElements = item.ElementExtensions
            .Where(ext => string.Equals(ext.OuterName, "image", StringComparison.OrdinalIgnoreCase));

        foreach (var ext in imageElements)
        {
            var element = ext.GetObject<XElement>();
            var url = element.Attribute("url")?.Value?.Trim();

            if (IsAbsoluteHttpUrl(url))
            {
                return url;
            }
        }

        return null;
    }

    private static string? ReadContentImageUrl(SyndicationItem item)
    {
        var raw = item.Summary?.Text;

        if (string.IsNullOrWhiteSpace(raw) && item.Content is TextSyndicationContent textContent)
        {
            raw = textContent.Text;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        using var document = parser.ParseDocument(raw);

        var imgSrc = document.QuerySelector("img[src]")
            ?.GetAttribute("src")
            ?.Trim();

        return IsAbsoluteHttpUrl(imgSrc) ? imgSrc : null;
    }

    private static string? ReadMediaRssImageUrl(SyndicationItem item)
    {
        foreach (var name in new[] { "thumbnail", "content" })
        {
            var elements = item.ElementExtensions.ReadElementExtensions<XElement>(name, MediaNamespace);
            var url = elements
                .Select(e => e.Attribute("url")?.Value?.Trim())
                .FirstOrDefault(u => IsAbsoluteHttpUrl(u));

            if (url is not null)
            {
                return url;
            }
        }

        var groups = item.ElementExtensions.ReadElementExtensions<XElement>("group", MediaNamespace);
        foreach (var group in groups)
        {
            foreach (var name in new[] { "thumbnail", "content" })
            {
                var url = group.Elements(XName.Get(name, MediaNamespace))
                    .Select(e => e.Attribute("url")?.Value?.Trim())
                    .FirstOrDefault(u => IsAbsoluteHttpUrl(u));

                if (url is not null)
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static bool IsAbsoluteHttpUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private static IReadOnlyList<string> ReadTags(SyndicationItem item)
    {
        var tags = new List<string>();
        var tagContainers = item.ElementExtensions.ReadElementExtensions<XElement>("tags", BarretNamespace);

        foreach (var tagContainer in tagContainers)
        {
            tags.AddRange(tagContainer
                .Elements()
                .Where(e => string.Equals(e.Name.LocalName, "tag", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag)));
        }

        if (tags.Count > 0)
        {
            return tags;
        }

        var categories = item.Categories
            .Select(c => c.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        if (categories.Count > 0)
        {
            return categories;
        }

        return ExtractInlineHashtags(ReadMediaDescription(item));
    }

    private static IReadOnlyList<string> ExtractInlineHashtags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return InlineHashtagPattern().Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ReadSummary(SyndicationItem item)
    {
        var raw = item.Summary?.Text;

        if (string.IsNullOrWhiteSpace(raw) && item.Content is TextSyndicationContent textContent)
        {
            raw = textContent.Text;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = ReadMediaDescription(item);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var stripped = StripHtml(raw);
        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    private static string? ReadMediaDescription(SyndicationItem item)
    {
        var descriptions = item.ElementExtensions.ReadElementExtensions<XElement>("description", MediaNamespace);
        var value = descriptions
            .Select(e => e.Value?.Trim())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (value is not null)
        {
            return value;
        }

        var groups = item.ElementExtensions.ReadElementExtensions<XElement>("group", MediaNamespace);
        foreach (var group in groups)
        {
            var desc = group.Elements(XName.Get("description", MediaNamespace))
                .Select(e => e.Value?.Trim())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            if (desc is not null)
            {
                return desc;
            }
        }

        return null;
    }

    private static string StripHtml(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        using var document = parser.ParseDocument(html);

        foreach (var element in document.QuerySelectorAll("script, style, noscript, svg, head"))
        {
            element.Remove();
        }

        return document.Body?.TextContent?.Trim() ?? string.Empty;
    }
}
