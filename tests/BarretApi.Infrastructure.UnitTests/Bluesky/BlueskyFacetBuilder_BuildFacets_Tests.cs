using BarretApi.Infrastructure.Bluesky;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Bluesky;

public class BlueskyFacetBuilder_BuildFacets_Tests
{
	[Fact]
	public void ReturnsNull_GivenNullText()
	{
		var result = BlueskyFacetBuilder.BuildFacets(null!);

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnsNull_GivenEmptyText()
	{
		var result = BlueskyFacetBuilder.BuildFacets(string.Empty);

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnsNull_GivenTextWithNoLinksOrHashtags()
	{
		var result = BlueskyFacetBuilder.BuildFacets("Just a plain text post");

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnsLinkFacet_GivenTextWithHttpsUrl()
	{
		var text = "Check out https://example.com/article for more info";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		result.ShouldContain(f =>
			f.Features[0].Type == "app.bsky.richtext.facet#link" &&
			f.Features[0].Uri == "https://example.com/article");
	}

	[Fact]
	public void ReturnsLinkFacet_GivenTextWithHttpUrl()
	{
		var text = "Visit http://example.com today";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		result.ShouldContain(f =>
			f.Features[0].Type == "app.bsky.richtext.facet#link" &&
			f.Features[0].Uri == "http://example.com");
	}

	[Fact]
	public void ComputesCorrectByteOffsets_GivenUrlInMiddleOfText()
	{
		var text = "See https://example.com here";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var linkFacet = result.ShouldHaveSingleItem();
		linkFacet.Index.ByteStart.ShouldBe(4); // "See " = 4 bytes
		linkFacet.Index.ByteEnd.ShouldBe(23);  // "https://example.com" = 19 bytes, 4+19=23
	}

	[Fact]
	public void TrimsTrailingPunctuation_GivenUrlFollowedByPeriod()
	{
		var text = "Visit https://example.com.";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var facet = result.ShouldHaveSingleItem();
		facet.Features[0].Uri.ShouldBe("https://example.com");
	}

	[Fact]
	public void TrimsTrailingPunctuation_GivenUrlFollowedByComma()
	{
		var text = "See https://example.com, then continue";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var facet = result.ShouldHaveSingleItem();
		facet.Features[0].Uri.ShouldBe("https://example.com");
	}

	[Fact]
	public void ReturnsBothLinkAndHashtagFacets_GivenTextWithBoth()
	{
		var text = "Read https://example.com/post #dotnet";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result.ShouldContain(f => f.Features[0].Type == "app.bsky.richtext.facet#link");
		result.ShouldContain(f => f.Features[0].Type == "app.bsky.richtext.facet#tag");
	}

	[Fact]
	public void ReturnsMultipleLinkFacets_GivenTextWithMultipleUrls()
	{
		var text = "Check https://one.com and https://two.com";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var links = result.Where(f => f.Features[0].Type == "app.bsky.richtext.facet#link").ToList();
		links.Count.ShouldBe(2);
		links.ShouldContain(f => f.Features[0].Uri == "https://one.com");
		links.ShouldContain(f => f.Features[0].Uri == "https://two.com");
	}

	[Fact]
	public void ReturnsHashtagFacet_GivenTextWithOnlyHashtag()
	{
		var text = "Hello #world";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var facet = result.ShouldHaveSingleItem();
		facet.Features[0].Type.ShouldBe("app.bsky.richtext.facet#tag");
		facet.Features[0].Tag.ShouldBe("world");
	}

	[Fact]
	public void ComputesCorrectByteOffsets_GivenUnicodeBeforeUrl()
	{
		var text = "Héllo https://example.com";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var facet = result.ShouldHaveSingleItem();
		// "Héllo " = H(1) + é(2) + l(1) + l(1) + o(1) + space(1) = 7 bytes
		facet.Index.ByteStart.ShouldBe(7);
	}

	[Fact]
	public void PreservesPathAndQueryString_GivenComplexUrl()
	{
		var text = "Read https://example.com/blog/post?id=123&ref=tw today";
		var result = BlueskyFacetBuilder.BuildFacets(text);

		result.ShouldNotBeNull();
		var facet = result.ShouldHaveSingleItem();
		facet.Features[0].Uri.ShouldBe("https://example.com/blog/post?id=123&ref=tw");
	}
}
