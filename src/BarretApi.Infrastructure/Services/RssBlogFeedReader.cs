using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class RssBlogFeedReader(
	HttpClient httpClient,
	IOptions<BlogPromotionOptions> blogPromotionOptions,
	ILogger<RssBlogFeedReader> logger) : IBlogFeedReader
{
	private const string BarretNamespace = "https://barretblake.dev/ns/";

	private readonly HttpClient _httpClient = httpClient;
	private readonly BlogPromotionOptions _options = blogPromotionOptions.Value;
	private readonly ILogger<RssBlogFeedReader> _logger = logger;

	public async Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(_options.FeedUrl);

		using var stream = await _httpClient.GetStreamAsync(_options.FeedUrl, cancellationToken);
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
				Summary = item.Summary?.Text,
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
		return heroElements
			.Select(element => element.Value?.Trim())
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
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

		return tags;
	}
}
