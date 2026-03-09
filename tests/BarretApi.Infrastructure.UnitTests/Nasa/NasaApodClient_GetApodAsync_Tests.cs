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
			ApiKey = "test-api-key",
			BaseUrl = "https://api.nasa.gov/planetary/apod"
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
			title = "The Aurora Tree",
			explanation = "Yes, but can your tree do this?",
			url = "https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg",
			hdurl = "https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg",
			media_type = "image",
			copyright = "Alyn Wallace",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(new DateOnly(2026, 3, 8));

		result.ShouldNotBeNull();
		result.Title.ShouldBe("The Aurora Tree");
		result.Date.ShouldBe(new DateOnly(2026, 3, 8));
		result.Explanation.ShouldBe("Yes, but can your tree do this?");
		result.Url.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg");
		result.HdUrl.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg");
		result.MediaType.ShouldBe(ApodMediaType.Image);
		result.Copyright.ShouldBe("Alyn Wallace");
	}

	[Fact]
	public async Task ReturnsVideoApodEntry_GivenVideoResponse()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-01",
			title = "Galaxy Video",
			explanation = "A cool galaxy video",
			url = "https://www.youtube.com/embed/abc123",
			media_type = "video",
			thumbnail_url = "https://img.youtube.com/vi/abc123/0.jpg",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(new DateOnly(2026, 3, 1));

		result.MediaType.ShouldBe(ApodMediaType.Video);
		result.ThumbnailUrl.ShouldBe("https://img.youtube.com/vi/abc123/0.jpg");
		result.HdUrl.ShouldBeNull();
	}

	[Fact]
	public async Task SendsThumbsTrue_GivenAnyRequest()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(null);

		handler.LastRequest.ShouldNotBeNull();
		handler.LastRequest!.RequestUri!.Query.ShouldContain("thumbs=True");
	}

	[Fact]
	public async Task SendsApiKey_GivenAnyRequest()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(null);

		handler.LastRequest!.RequestUri!.Query.ShouldContain("api_key=test-api-key");
	}

	[Fact]
	public async Task SendsDateParameter_GivenSpecificDate()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-02-14",
			title = "Valentine Nebula",
			explanation = "A lovely nebula",
			url = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		await sut.GetApodAsync(new DateOnly(2026, 2, 14));

		handler.LastRequest!.RequestUri!.Query.ShouldContain("date=2026-02-14");
	}

	[Fact]
	public async Task OmitsDateParameter_GivenNullDate()
	{
		var json = JsonSerializer.Serialize(new
		{
			date = "2026-03-08",
			title = "Test",
			explanation = "Test",
			url = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
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
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
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
			url = "https://example.com/img.jpg",
			media_type = "image",
			service_version = "v1"
		});
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		});
		using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.nasa.gov") };
		var sut = CreateClient(httpClient);

		var result = await sut.GetApodAsync(null);

		result.Copyright.ShouldBeNull();
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
