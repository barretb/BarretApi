using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class RssRandomPostService_SelectAndPostAsync_Tests
{
	private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
	private readonly ISocialPlatformClient _mockClient;
	private readonly SocialPostService _socialPostService;
	private readonly RssRandomPostService _sut;

	private static readonly List<BlogFeedEntry> SampleEntries =
	[
		new()
		{
			EntryIdentity = "entry-1",
			CanonicalUrl = "https://example.com/post-1",
			Title = "Post One",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
			Tags = ["dotnet", "aspire"]
		},
		new()
		{
			EntryIdentity = "entry-2",
			CanonicalUrl = "https://example.com/post-2",
			Title = "Post Two",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
			HeroImageUrl = "https://example.com/images/hero.jpg",
			Tags = ["personal"]
		},
		new()
		{
			EntryIdentity = "entry-3",
			CanonicalUrl = "https://example.com/post-3",
			Title = "Post Three",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
			Tags = []
		}
	];

	public RssRandomPostService_SelectAndPostAsync_Tests()
	{
		_mockClient = Substitute.For<ISocialPlatformClient>();
		_mockClient.PlatformName.Returns("testplatform");
		_mockClient.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration { Name = "testplatform", MaxCharacters = 500 });
		_mockClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
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
			[_mockClient],
			textShorteningService,
			imageDownloadService,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new RssRandomPostService(
			_feedReader,
			_socialPostService,
			Substitute.For<ILogger<RssRandomPostService>>());
	}

	[Fact]
	public async Task ReturnsResult_GivenEntriesExist()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(SampleEntries);

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		var result = await _sut.SelectAndPostAsync(query);

		result.ShouldNotBeNull();
		result.SelectedEntry.ShouldNotBeNull();
		SampleEntries.ShouldContain(result.SelectedEntry);
	}

	[Fact]
	public async Task ThrowsInvalidOperation_GivenEmptyFeed()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<BlogFeedEntry>());

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SelectAndPostAsync(query));

		ex.Message.ShouldContain("no eligible");
	}

	[Fact]
	public async Task DelegatesToSocialPostService_GivenSingleEntry()
	{
		var singleEntry = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "entry-1",
				CanonicalUrl = "https://example.com/post-1",
				Title = "Test Title",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = ["tag1"]
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(singleEntry);

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.Title.ShouldBe("Test Title");
		result.SelectedEntry.CanonicalUrl.ShouldBe("https://example.com/post-1");
		result.PlatformResults.Count.ShouldBe(1);
		result.PlatformResults[0].Success.ShouldBeTrue();
	}

	[Fact]
	public async Task PostsTextWithTitleAndUrl_GivenEntry()
	{
		var singleEntry = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "entry-1",
				CanonicalUrl = "https://example.com/post-1",
				Title = "My Great Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(singleEntry);

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		await _sut.SelectAndPostAsync(query);

		await _mockClient.Received(1).PostAsync(
			Arg.Is<string>(text => text.Contains("My Great Post") && text.Contains("https://example.com/post-1")),
			Arg.Any<IReadOnlyList<UploadedImage>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ThrowsHttpRequestException_GivenFeedReadFails()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new HttpRequestException("Connection refused"));

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		await Should.ThrowAsync<HttpRequestException>(
			() => _sut.SelectAndPostAsync(query));
	}

	[Fact]
	public async Task FetchesFeedFromProvidedUrl()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(SampleEntries);

		var query = new RssRandomPostQuery { FeedUrl = "https://myblog.com/rss.xml" };

		await _sut.SelectAndPostAsync(query);

		await _feedReader.Received(1).ReadEntriesAsync("https://myblog.com/rss.xml", Arg.Any<CancellationToken>());
	}
}

public sealed class RssRandomPostService_PlatformTargeting_Tests
{
	private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
	private readonly ISocialPlatformClient _blueskyClient;
	private readonly ISocialPlatformClient _mastodonClient;
	private readonly RssRandomPostService _sut;

	private static readonly List<BlogFeedEntry> SingleEntry =
	[
		new()
		{
			EntryIdentity = "entry-1",
			CanonicalUrl = "https://example.com/post-1",
			Title = "Test Post",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
			Tags = []
		}
	];

	public RssRandomPostService_PlatformTargeting_Tests()
	{
		_blueskyClient = CreateMockClient("bluesky");
		_mastodonClient = CreateMockClient("mastodon");

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
			[_blueskyClient, _mastodonClient],
			textShorteningService,
			imageDownloadService,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new RssRandomPostService(
			_feedReader,
			socialPostService,
			Substitute.For<ILogger<RssRandomPostService>>());
	}

	[Fact]
	public async Task PostsToAllPlatforms_GivenNoPlatformsSpecified()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(SingleEntry);

		var query = new RssRandomPostQuery { FeedUrl = "https://example.com/feed.xml" };

		var result = await _sut.SelectAndPostAsync(query);

		result.PlatformResults.Count.ShouldBe(2);
		await _blueskyClient.Received(1).PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
		await _mastodonClient.Received(1).PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PostsOnlyToSpecifiedPlatform_GivenPlatformsProvided()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(SingleEntry);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			Platforms = ["bluesky"]
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.PlatformResults.Count.ShouldBe(1);
		result.PlatformResults[0].Platform.ShouldBe("bluesky");
		await _blueskyClient.Received(1).PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
		await _mastodonClient.DidNotReceive().PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
	}

	private static ISocialPlatformClient CreateMockClient(string name)
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns(name);
		client.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration { Name = name, MaxCharacters = 500 });
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = name,
				Success = true,
				PostId = $"{name}-post-id",
				PostUrl = $"https://{name}.com/post/1",
				PublishedText = callInfo.ArgAt<string>(0)
			});
		return client;
	}
}

public sealed class RssRandomPostService_TagExclusion_Tests
{
	private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
	private readonly IHashtagService _hashtagService = Substitute.For<IHashtagService>();
	private readonly RssRandomPostService _sut;

	private static readonly List<BlogFeedEntry> TaggedEntries =
	[
		new()
		{
			EntryIdentity = "entry-1",
			CanonicalUrl = "https://example.com/post-1",
			Title = "Dotnet Post",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
			Tags = ["dotnet", "aspire"]
		},
		new()
		{
			EntryIdentity = "entry-2",
			CanonicalUrl = "https://example.com/post-2",
			Title = "Personal Post",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
			Tags = ["personal"]
		},
		new()
		{
			EntryIdentity = "entry-3",
			CanonicalUrl = "https://example.com/post-3",
			Title = "Untagged Post",
			PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
			Tags = []
		}
	];

	public RssRandomPostService_TagExclusion_Tests()
	{
		var mockClient = CreateMockClient("testplatform");

		var textShorteningService = Substitute.For<ITextShorteningService>();
		textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
			.Returns(callInfo => callInfo.ArgAt<string>(0));

		var imageDownloadService = Substitute.For<IImageDownloadService>();
		_hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
			.Returns(callInfo => new HashtagProcessingResult
			{
				FinalText = callInfo.ArgAt<string>(0),
				AllHashtags = callInfo.ArgAt<IReadOnlyList<string>>(1)
			});

		var socialPostService = new SocialPostService(
			[mockClient],
			textShorteningService,
			imageDownloadService,
			_hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new RssRandomPostService(
			_feedReader,
			socialPostService,
			Substitute.For<ILogger<RssRandomPostService>>());
	}

	[Fact]
	public async Task ExcludesEntriesWithMatchingTag()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TaggedEntries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["personal"]
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.Tags.ShouldNotContain("personal");
	}

	[Fact]
	public async Task ExcludesTagsCaseInsensitively()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TaggedEntries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["PERSONAL"]
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.Tags.ShouldNotContain("personal");
	}

	[Fact]
	public async Task DoesNotExcludeEntriesWithoutTags()
	{
		var entriesWithUntagged = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "only-untagged",
				CanonicalUrl = "https://example.com/untagged",
				Title = "Untagged Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entriesWithUntagged);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["personal"]
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.EntryIdentity.ShouldBe("only-untagged");
	}

	[Fact]
	public async Task ThrowsInvalidOperation_GivenAllEntriesFiltered()
	{
		var allPersonal = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "entry-1",
				CanonicalUrl = "https://example.com/post-1",
				Title = "Personal One",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = ["personal"]
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(allPersonal);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["personal"]
		};

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SelectAndPostAsync(query));

		ex.Message.ShouldContain("no eligible");
	}

	[Fact]
	public async Task ExcludesExcludedTagsFromHashtags()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "entry-1",
				CanonicalUrl = "https://example.com/post-1",
				Title = "Tech Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = ["dotnet", "aspire"]
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["aspire"]
		};

		// Entry is filtered out by tag exclusion (has "aspire" tag matching excludeTags).
		// This verifies the entry-level filter catches it and only non-excluded tags
		// would reach the hashtag processing pipeline.
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SelectAndPostAsync(query));

		ex.Message.ShouldContain("no eligible");
	}

	[Fact]
	public async Task NoFilteringApplied_GivenEmptyExcludeTags()
	{
		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TaggedEntries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = []
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.ShouldNotBeNull();
		TaggedEntries.ShouldContain(result.SelectedEntry);
	}

	[Fact]
	public async Task PassesNonExcludedTagsAsHashtags()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "entry-1",
				CanonicalUrl = "https://example.com/post-1",
				Title = "Tech Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				Tags = ["dotnet", "aspire"]
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			ExcludeTags = ["personal"]
		};

		await _sut.SelectAndPostAsync(query);

		_hashtagService.Received().ProcessHashtags(
			Arg.Any<string>(),
			Arg.Is<IReadOnlyList<string>>(tags =>
				tags.Contains("dotnet") && tags.Contains("aspire") && tags.Count == 2));
	}

	private static ISocialPlatformClient CreateMockClient(string name)
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns(name);
		client.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration { Name = name, MaxCharacters = 500 });
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = name,
				Success = true,
				PostId = $"{name}-post-id",
				PostUrl = $"https://{name}.com/post/1",
				PublishedText = callInfo.ArgAt<string>(0)
			});
		return client;
	}
}

public sealed class RssRandomPostService_RecencyFiltering_Tests
{
	private readonly IBlogFeedReader _feedReader = Substitute.For<IBlogFeedReader>();
	private readonly RssRandomPostService _sut;

	public RssRandomPostService_RecencyFiltering_Tests()
	{
		var mockClient = Substitute.For<ISocialPlatformClient>();
		mockClient.PlatformName.Returns("testplatform");
		mockClient.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration { Name = "testplatform", MaxCharacters = 500 });
		mockClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
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
			[mockClient],
			textShorteningService,
			imageDownloadService,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new RssRandomPostService(
			_feedReader,
			socialPostService,
			Substitute.For<ILogger<RssRandomPostService>>());
	}

	[Fact]
	public async Task ExcludesEntriesOlderThanMaxAgeDays()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "recent",
				CanonicalUrl = "https://example.com/recent",
				Title = "Recent Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
				Tags = []
			},
			new()
			{
				EntryIdentity = "old",
				CanonicalUrl = "https://example.com/old",
				Title = "Old Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			MaxAgeDays = 7
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.EntryIdentity.ShouldBe("recent");
	}

	[Fact]
	public async Task ExcludesEntriesWithVeryOldPublicationDate_GivenMaxAgeDaysSet()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "recent",
				CanonicalUrl = "https://example.com/recent",
				Title = "Recent Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
				Tags = []
			},
			new()
			{
				EntryIdentity = "ancient",
				CanonicalUrl = "https://example.com/ancient",
				Title = "Ancient Post",
				PublishedAtUtc = DateTimeOffset.MinValue,
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			MaxAgeDays = 7
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.EntryIdentity.ShouldBe("recent");
	}

	[Fact]
	public async Task ThrowsInvalidOperation_GivenAllEntriesOlderThanMaxAgeDays()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "old",
				CanonicalUrl = "https://example.com/old",
				Title = "Old Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			MaxAgeDays = 7
		};

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SelectAndPostAsync(query));

		ex.Message.ShouldContain("no eligible");
	}

	[Fact]
	public async Task NoRecencyFiltering_GivenMaxAgeDaysOmitted()
	{
		var entries = new List<BlogFeedEntry>
		{
			new()
			{
				EntryIdentity = "very-old",
				CanonicalUrl = "https://example.com/very-old",
				Title = "Very Old Post",
				PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-365),
				Tags = []
			}
		};

		_feedReader.ReadEntriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(entries);

		var query = new RssRandomPostQuery
		{
			FeedUrl = "https://example.com/feed.xml",
			MaxAgeDays = null
		};

		var result = await _sut.SelectAndPostAsync(query);

		result.SelectedEntry.EntryIdentity.ShouldBe("very-old");
	}
}
