using System.Net;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Nasa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Nasa;

public sealed class NasaApodClient_GetApodAsync_Tests
{
	private readonly IOptions<NasaApodOptions> _options;
	private readonly ILogger<NasaApodClient> _logger = Substitute.For<ILogger<NasaApodClient>>();

	public NasaApodClient_GetApodAsync_Tests()
	{
		_options = Options.Create(new NasaApodOptions
		{
		});
	}

	private NasaApodClient CreateClient(HttpClient httpClient)
	{
		return new NasaApodClient(httpClient, _options, _logger);
	}

	[Fact]
	public async Task ReturnsApodEntry_GivenSuccessfulImageResponse()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "The Aurora &amp; Tree",
			explanation = "<p>Yes, but can <a href=\"https://example.com\">your tree</a> do this?</p>",
			url = "https://science.nasa.gov/image-article/aurora-tree/",
			hdurl = "https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg",
			media_type = "image",
			copyright = "<b>Alyn Wallace</b>",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(new DateOnly(2026, 3, 8));

		result.ShouldNotBeNull();
		result.Title.ShouldBe("The Aurora & Tree");
		result.Date.ShouldBe(new DateOnly(2026, 3, 8));
		result.Explanation.ShouldBe("Yes, but can your tree do this?");
		result.Url.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg");
		result.HdUrl.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg");
		result.MediaType.ShouldBe(ApodMediaType.Image);
		result.Copyright.ShouldBe("Alyn Wallace");
	}

	[Theory]
	[InlineData("video")]
	[InlineData("iframe")]
	public async Task ReturnsVideoApodEntry_GivenVideoResponse(string mediaType)
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-01",
			title = "Galaxy Video",
			explanation = "A cool galaxy video",
			url = "https://www.youtube.com/embed/abc123",
			media_type = mediaType,
			hdurl = "https://img.youtube.com/vi/abc123/0.jpg",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(new DateOnly(2026, 3, 1));

		result.MediaType.ShouldBe(ApodMediaType.Video);
		result.ThumbnailUrl.ShouldBe("https://img.youtube.com/vi/abc123/0.jpg");
		result.HdUrl.ShouldBeNull();
	}

	[Fact]
	public async Task UsesNewEndpoint_GivenDefaultOptions()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://science.nasa.gov/image-article/example/",
			hdurl = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(null);

		handler.LastRequest.ShouldNotBeNull();
		handler.LastRequest!.RequestUri!.AbsoluteUri.ShouldBe("https://science.nasa.gov/wp-json/wp/v2/apod-basic?per_page=1");
	}

	[Fact]
	public async Task OmitsLegacyParameters_GivenAnyRequest()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://science.nasa.gov/image-article/example/",
			hdurl = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(null);

		handler.LastRequest!.RequestUri!.Query.ShouldNotContain("api_key");
		handler.LastRequest!.RequestUri!.Query.ShouldNotContain("thumbs");
	}

	[Fact]
	public async Task UsesDatePath_GivenSpecificDate()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-02-14",
			title = "Valentine Nebula",
			explanation = "A lovely nebula",
			url = "https://science.nasa.gov/image-article/example/",
			hdurl = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(new DateOnly(2026, 2, 14));

		handler.LastRequest!.RequestUri!.AbsolutePath.ShouldBe("/wp-json/wp/v2/apod-basic/260214");
	}

	[Fact]
	public async Task OmitsDateParameter_GivenNullDate()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://science.nasa.gov/image-article/example/",
			hdurl = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(null);

		handler.LastRequest!.RequestUri!.Query.ShouldNotContain("date=");
	}

	[Theory]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.InternalServerError)]
	public async Task ThrowsHttpRequestException_GivenErrorStatusCode(HttpStatusCode statusCode)
	{
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(statusCode)
		{
			Content = new StringContent("{\"error\":\"fail\"}", System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		await Should.ThrowAsync<HttpRequestException>(() => sut.GetApodAsync(null));
	}

	[Fact]
	public async Task ReturnsNullCopyright_GivenPublicDomainImage()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "NASA Public Image",
			explanation = "Free to use",
			url = "https://science.nasa.gov/image-article/example/",
			hdurl = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler);
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(null);

		result.Copyright.ShouldBeNull();
	}

	[Fact]
	public async Task RejectsArticleDownload_GivenImageWithoutHdUrl()
	{
		using var httpClient = new HttpClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("""
				{"date":"2026-09-20","title":"Test","explanation":"Test",
				 "url":"https://science.nasa.gov/image-article/example/","media_type":"image"}
				""")
		}));
		var sut = CreateClient(httpClient);

		await Should.ThrowAsync<InvalidOperationException>(() => sut.GetApodAsync(null));
	}

	[Fact]
	public async Task ReturnsArticleLinkWithoutImage_GivenVideoWithoutThumbnail()
	{
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("""
				{"date":"2026-09-20","title":"Test","explanation":"Test",
				 "url":"https://science.nasa.gov/image-article/example/","media_type":"iframe"}
				""")
		});
		using var httpClient = new HttpClient(handler);
		var sut = new NasaApodClient(httpClient,
			Options.Create(new NasaApodOptions { BaseUrl = "https://example.com/apod" }), _logger);

		var result = await sut.GetApodAsync(new DateOnly(2026, 9, 20));

		result.Url.ShouldBe("https://science.nasa.gov/image-article/example/");
		result.HdUrl.ShouldBeNull();
		result.ThumbnailUrl.ShouldBeNull();
		result.MediaType.ShouldBe(ApodMediaType.Video);
		handler.LastRequest!.RequestUri!.AbsoluteUri.ShouldBe("https://example.com/apod/260920");
	}

	[Fact]
	public async Task ReturnsLatestEntry_GivenCollectionResponse()
	{
		using var httpClient = new HttpClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("""
				[{"date":"2026-09-28","title":"Latest","explanation":"Test",
				  "url":"https://science.nasa.gov/image-article/example/",
				  "hdurl":"https://example.com/image.jpg","media_type":"image"}]
				""")
		}));
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(null);

		result.Date.ShouldBe(new DateOnly(2026, 9, 28));
		result.Title.ShouldBe("Latest");
		result.Url.ShouldBe("https://example.com/image.jpg");
	}

	[Fact]
	public async Task ThrowsInvalidOperationException_GivenEmptyCollection()
	{
		using var httpClient = new HttpClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("[]")
		}));
		var sut = CreateClient(httpClient);

		await Should.ThrowAsync<InvalidOperationException>(() => sut.GetApodAsync(null));
	}

	private sealed class FakeHttpMessageHandler : HttpMessageHandler
	{
		private readonly HttpResponseMessage _response;

		public FakeHttpMessageHandler(HttpResponseMessage response)
		{
			_response = response;
		}

		public HttpRequestMessage? LastRequest { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(_response);
		}
	}
}
