using System.Net;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Nasa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Nasa;

public sealed class NasaGibsClient_GetSnapshotAsync_Tests
{
	private readonly IOptions<NasaGibsOptions> _options;
	private readonly ILogger<NasaGibsClient> _logger = Substitute.For<ILogger<NasaGibsClient>>();

	public NasaGibsClient_GetSnapshotAsync_Tests()
	{
		_options = Options.Create(new NasaGibsOptions());
	}

	private NasaGibsClient CreateClient(HttpClient httpClient)
	{
		return new NasaGibsClient(httpClient, _options, _logger);
	}

	private static GibsSnapshotRequest CreateRequest(
		string layer = "MODIS_Terra_CorrectedReflectance_TrueColor",
		DateOnly? date = null,
		double bboxSouth = 38.4,
		double bboxWest = -84.82,
		double bboxNorth = 42.32,
		double bboxEast = -80.52,
		int imageWidth = 1024,
		int imageHeight = 768)
	{
		return new GibsSnapshotRequest(
			layer,
			date ?? new DateOnly(2026, 3, 7),
			bboxSouth,
			bboxWest,
			bboxNorth,
			bboxEast,
			imageWidth,
			imageHeight);
	}

	[Fact]
	public async Task ReturnsGibsSnapshotEntry_GivenSuccessfulJpegResponse()
	{
		var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new ByteArrayContent(imageBytes)
			{
				Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
			}
		});
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		var result = await sut.GetSnapshotAsync(CreateRequest());

		result.ShouldNotBeNull();
		result.ImageBytes.ShouldBe(imageBytes);
		result.Date.ShouldBe(new DateOnly(2026, 3, 7));
		result.Layer.ShouldBe("MODIS_Terra_CorrectedReflectance_TrueColor");
		result.Width.ShouldBe(1024);
		result.Height.ShouldBe(768);
		result.ContentType.ShouldBe("image/jpeg");
	}

	[Fact]
	public async Task ThrowsInvalidOperationException_GivenXmlErrorResponse()
	{
		var xmlError = """
			<?xml version='1.0' encoding="UTF-8"?>
			<ServiceExceptionReport>
			  <ServiceException code="LayerNotDefined">
			    msWMSLoadGetMapParams(): Invalid layer(s).
			  </ServiceException>
			</ServiceExceptionReport>
			""";
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(xmlError, System.Text.Encoding.UTF8, "text/xml")
		});
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GetSnapshotAsync(CreateRequest(layer: "INVALID_LAYER")));

		ex.Message.ShouldContain("GIBS snapshot returned an error");
		ex.Message.ShouldContain("ServiceException");
	}

	[Fact]
	public async Task ThrowsHttpRequestException_GivenServerError()
	{
		var handler = new FakeHttpMessageHandler(
			new HttpResponseMessage(HttpStatusCode.InternalServerError));
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		await Should.ThrowAsync<HttpRequestException>(
			() => sut.GetSnapshotAsync(CreateRequest()));
	}

	[Fact]
	public async Task ConstructsCorrectUrl_GivenLayerAndDate()
	{
		var imageBytes = new byte[] { 0xFF, 0xD8 };
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new ByteArrayContent(imageBytes)
			{
				Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
			}
		});
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		await sut.GetSnapshotAsync(CreateRequest(
			layer: "VIIRS_SNPP_CorrectedReflectance_TrueColor",
			date: new DateOnly(2026, 2, 14)));

		var url = handler.LastRequest!.RequestUri!.ToString();
		url.ShouldContain("REQUEST=GetSnapshot");
		url.ShouldContain("SERVICE=WMS");
		url.ShouldContain("LAYERS=VIIRS_SNPP_CorrectedReflectance_TrueColor");
		url.ShouldContain("CRS=EPSG:4326");
		url.ShouldContain("BBOX=38.4,-84.82,42.32,-80.52");
		url.ShouldContain("FORMAT=image/jpeg");
		url.ShouldContain("WIDTH=1024");
		url.ShouldContain("HEIGHT=768");
		url.ShouldContain("TIME=2026-02-14");
	}

	[Fact]
	public async Task ThrowsInvalidOperationException_GivenTextPlainErrorResponse()
	{
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("Error: service unavailable", System.Text.Encoding.UTF8, "text/plain")
		});
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GetSnapshotAsync(CreateRequest()));

		ex.Message.ShouldContain("GIBS snapshot returned an error");
	}

	[Fact]
	public async Task ThrowsWithCleanMessage_GivenHtmlErrorResponse()
	{
		var htmlBody = "<!doctype html><html lang=\"en\"><head><title>Error</title></head><body>Not Found</body></html>";
		var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(htmlBody, System.Text.Encoding.UTF8, "text/html")
		});
		using var httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://wvs.earthdata.nasa.gov")
		};
		var sut = CreateClient(httpClient);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.GetSnapshotAsync(CreateRequest()));

		ex.Message.ShouldContain("unexpected HTML response");
		ex.Message.ShouldContain("Verify the configured base URL");
		ex.Message.ShouldNotContain("<!doctype");
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
