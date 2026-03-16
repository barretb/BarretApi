using System.Net;
using BarretApi.Infrastructure.DiceBear;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.DiceBear;

public sealed class DiceBearAvatarClient_GetAvatarAsync_Tests
{
    private readonly ILogger<DiceBearAvatarClient> _logger = Substitute.For<ILogger<DiceBearAvatarClient>>();

    private DiceBearAvatarClient CreateClient(HttpClient httpClient)
    {
        return new DiceBearAvatarClient(httpClient, _logger);
    }

    [Fact]
    public async Task ReturnsAvatarResult_GivenSuccessfulResponse()
    {
        var imageBytes = new byte[] { 0x3C, 0x73, 0x76, 0x67 };
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(imageBytes)
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        var result = await sut.GetAvatarAsync("pixel-art", "svg", "test-seed");

        result.ShouldNotBeNull();
        result.ImageBytes.ShouldBe(imageBytes);
        result.ContentType.ShouldBe("image/svg+xml");
        result.Style.ShouldBe("pixel-art");
        result.Seed.ShouldBe("test-seed");
        result.Format.ShouldBe("svg");
    }

    [Fact]
    public async Task SendsCorrectUrl_GivenStyleFormatAndSeed()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x01])
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        await sut.GetAvatarAsync("bottts", "png", "my-seed");

        handler.LastRequest.ShouldNotBeNull();
        var url = handler.LastRequest!.RequestUri!.ToString();
        url.ShouldContain("9.x/bottts/png");
        url.ShouldContain("seed=my-seed");
    }

    [Fact]
    public async Task UsesRandomStyleAndSeed_GivenNoParameters()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x01])
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        var result = await sut.GetAvatarAsync();

        result.Style.ShouldNotBeNullOrWhiteSpace();
        result.Seed.ShouldNotBeNullOrWhiteSpace();
        result.Format.ShouldBe("svg");
        result.ContentType.ShouldBe("image/svg+xml");
    }

    [Fact]
    public async Task ThrowsInvalidOperationException_GivenUpstreamErrorResponse()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAvatarAsync("pixel-art", "svg", "test"));

        exception.Message.ShouldContain("temporarily unavailable");
    }

    [Fact]
    public async Task ThrowsInvalidOperationException_GivenHttpRequestException()
    {
        var handler = new FakeThrowingHttpMessageHandler(
            new HttpRequestException("Connection refused"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAvatarAsync("pixel-art", "svg", "test"));

        exception.Message.ShouldContain("temporarily unavailable");
    }

    [Fact]
    public async Task UrlEncodesSeed_GivenSpecialCharacters()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x01])
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        await sut.GetAvatarAsync("bottts", "svg", "hello world&foo=bar");

        handler.LastRequest.ShouldNotBeNull();
        var url = handler.LastRequest!.RequestUri!.AbsoluteUri;
        url.ShouldContain("seed=hello%20world%26foo%3Dbar");
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    [InlineData("avif", "image/avif")]
    [InlineData("svg", "image/svg+xml")]
    public async Task ReturnsCorrectContentType_GivenFormat(string format, string expectedContentType)
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x01])
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.dicebear.com/")
        };
        var sut = CreateClient(httpClient);

        var result = await sut.GetAvatarAsync("adventurer", format, "test");

        result.ContentType.ShouldBe(expectedContentType);
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response = response;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private sealed class FakeThrowingHttpMessageHandler(HttpRequestException exception) : HttpMessageHandler
    {
        private readonly HttpRequestException _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
