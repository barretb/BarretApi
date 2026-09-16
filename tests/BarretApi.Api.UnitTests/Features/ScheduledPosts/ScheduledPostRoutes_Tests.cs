using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BarretApi.Api.Auth;
using BarretApi.Api.Features.ScheduledPosts;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.ScheduledPosts;

public sealed class ScheduledPostRoutes_Tests : IAsyncDisposable
{
	private const string Key = "post-management-test-key";
	private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
	private readonly IScheduledSocialPostRepository _repository = Substitute.For<IScheduledSocialPostRepository>();
	private readonly WebApplication _app;
	private readonly HttpClient _client;
	private readonly ScheduledSocialPostRecord _record = new()
	{
		ScheduledPostId = "post",
		Status = ScheduledPostStatus.Pending,
		Text = "original",
		CreatedAtUtc = Now.AddDays(-1),
		ScheduledForUtc = Now.AddHours(1),
		TargetPlatforms = ["bluesky", "mastodon"],
		Version = "v1",
		UploadedImages = [new StoredImageData { BlobName = "private-blob-name", AltText = "image", ContentType = "image/png" }]
	};

	public ScheduledPostRoutes_Tests()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		_repository.GetByIdAsync("post", Arg.Any<CancellationToken>()).Returns(_record);
		_repository.ListAsync(Arg.Any<ScheduledPostsQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ScheduledPostsPage([_record], "next-page"));
		var revision = 1;
		_repository.TryUpdateAsync(Arg.Any<ScheduledSocialPostRecord>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			call.Arg<ScheduledSocialPostRecord>()!.Version = $"v{++revision}";
			return true;
		});
		builder.Services.AddSingleton(_repository);
		builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
		foreach (var name in new[] { "bluesky", "mastodon" })
		{
			var platform = Substitute.For<ISocialPlatformClient>();
			platform.PlatformName.Returns(name);
			builder.Services.AddSingleton(platform);
		}

		builder.Services.AddSingleton<ScheduledPostManagementService>();
		builder.Configuration["Auth:ApiKey"] = Key;
		builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
		builder.Services.AddAuthentication(ApiKeyAuthHandler.SchemeName)
			.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);
		builder.Services.AddAuthorization();
		builder.Services.AddFastEndpoints(options =>
		{
			options.Assemblies = [typeof(ListScheduledPostsEndpoint).Assembly];
			options.Filter = type => type.Namespace == typeof(ListScheduledPostsEndpoint).Namespace;
		});
		_app = builder.Build();
		_app.UseAuthentication();
		_app.UseAuthorization();
		_app.UseFastEndpoints();
		_app.StartAsync().GetAwaiter().GetResult();
		_client = _app.GetTestClient();
		_client.DefaultRequestHeaders.Add(ApiKeyAuthHandler.HeaderName, Key);
	}

	[Theory]
	[InlineData("GET", "")]
	[InlineData("GET", "/post")]
	[InlineData("PATCH", "/post")]
	[InlineData("POST", "/post/cancel")]
	[InlineData("POST", "/post/reconcile")]
	[InlineData("POST", "/post/retry")]
	public async Task RequiresAuthentication_GivenManagementRoute(string method, string path)
	{
		_client.DefaultRequestHeaders.Remove(ApiKeyAuthHandler.HeaderName);
		using var request = new HttpRequestMessage(new HttpMethod(method), "/api/social-posts/scheduled" + path)
		{
			Content = JsonContent.Create(new { })
		};

		using var response = await _client.SendAsync(request);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ListsSummariesAndToken_GivenFilters()
	{
		using var response = await _client.GetAsync("/api/social-posts/scheduled?status=Pending&pageSize=10&continuationToken=cursor&from=2026-09-01&to=2026-09-30");
		var payload = await response.Content.ReadFromJsonAsync<ListScheduledPostsResponse>();

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		payload!.Posts.Single().ScheduledPostId.ShouldBe("post");
		payload.ContinuationToken.ShouldBe("next-page");
		await _repository.Received(1).ListAsync(Arg.Is<ScheduledPostsQuery>(q => q != null
			&& q.Status == ScheduledPostStatus.Pending && q.PageSize == 10 && q.ContinuationToken == "cursor"
			&& q.FromUtc.HasValue && q.ToUtc.HasValue), Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData("status=unknown")]
	[InlineData("status=0")]
	[InlineData("pageSize=0")]
	[InlineData("pageSize=101")]
	[InlineData("from=2026-09-30&to=2026-09-01")]
	public async Task RejectsInvalidFilters_GivenListRequest(string query)
	{
		using var response = await _client.GetAsync("/api/social-posts/scheduled?" + query);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await _repository.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
	}

	[Fact]
	public async Task ReturnsDetailsWithoutStorageInternals_GivenKnownPost()
	{
		_record.DeliveryResults.Add(new PlatformPostResult
		{
			Platform = "bluesky",
			Success = false,
			ErrorCode = "RATE_LIMITED",
			Error = new Exception("private-exception")
		});

		using var response = await _client.GetAsync("/api/social-posts/scheduled/post");
		var text = await response.Content.ReadAsStringAsync();
		var payload = JsonSerializer.Deserialize<ScheduledPostDetailsResponse>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		payload!.Post.Version.ShouldBe("v1");
		payload.Images.Single().Source.ShouldBe("upload");
		text.ShouldNotContain("private-blob-name");
		text.ShouldNotContain("private-exception");
	}

	[Fact]
	public async Task Returns404_GivenUnknownPost()
	{
		using var response = await _client.GetAsync("/api/social-posts/scheduled/missing");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task EditsRouteIdentity_GivenDifferentBodyIdentity()
	{
		using var response = await _client.PatchAsJsonAsync("/api/social-posts/scheduled/post",
			new { id = "another-post", version = "v1", text = "changed", autoThread = true });

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		_record.Text.ShouldBe("changed");
		_record.AutoThread.ShouldBeTrue();
		await _repository.Received(1).GetByIdAsync("post", Arg.Any<CancellationToken>());
		await _repository.DidNotReceive().GetByIdAsync("another-post", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ReturnsConflict_GivenStaleVersion()
	{
		using var response = await _client.PostAsJsonAsync("/api/social-posts/scheduled/post/cancel",
			new { version = "stale", note = "cancel" });

		response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		_record.Status.ShouldBe(ScheduledPostStatus.Pending);
	}

	[Fact]
	public async Task CancelsPost_GivenCurrentVersion()
	{
		using var response = await _client.PostAsJsonAsync("/api/social-posts/scheduled/post/cancel",
			new { version = "v1", note = "Launch postponed" });
		var payload = await response.Content.ReadFromJsonAsync<ScheduledPostDetailsResponse>();

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		payload!.Post.Status.ShouldBe("Cancelled");
		payload.Post.Version.ShouldBe("v2");
	}

	[Theory]
	[InlineData("""{"text":"changed"}""")]
	[InlineData("""{"version":"*","text":"changed"}""")]
	[InlineData("""{"version":"v1"}""")]
	[InlineData("""{"version":"v1","images":[null]}""")]
	[InlineData("""{"version":"v1","platforms":["bluesky",null]}""")]
	public async Task RejectsInvalidEdit_GivenMalformedBody(string json)
	{
		using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/social-posts/scheduled/post")
		{
			Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
		};

		using var response = await _client.SendAsync(request);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		await _repository.DidNotReceiveWithAnyArgs().TryUpdateAsync(default!, default);
	}

	[Fact]
	public async Task ReconcilesThenQueuesOnlyRemainingPlatform_GivenVerifiedRemoteState()
	{
		_record.Status = ScheduledPostStatus.NeedsReview;
		using var confirmation = await _client.PostAsJsonAsync("/api/social-posts/scheduled/post/reconcile", new
		{
			version = "v1",
			note = "Checked the complete published post",
			publishedDeliveries = new[] { new { platform = "bluesky", postId = "remote-id", postUrl = "https://example.com/post" } }
		});
		confirmation.StatusCode.ShouldBe(HttpStatusCode.OK);
		_record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);

		using var retry = await _client.PostAsJsonAsync("/api/social-posts/scheduled/post/retry", new
		{
			version = "v2",
			platforms = new[] { "mastodon" },
			confirmNotPublished = true,
			note = "No Mastodon post exists"
		});

		retry.StatusCode.ShouldBe(HttpStatusCode.OK);
		_record.Status.ShouldBe(ScheduledPostStatus.Pending);
		_record.DeliveryResults.Single().PostId.ShouldBe("remote-id");
		_record.DeliveryConfirmations.Single().Platform.ShouldBe("bluesky");
	}

	[Fact]
	public async Task RejectsUnconfirmedRetry_GivenMissingAcknowledgment()
	{
		_record.Status = ScheduledPostStatus.NeedsReview;
		using var response = await _client.PostAsJsonAsync("/api/social-posts/scheduled/post/retry", new
		{
			version = "v1",
			platforms = new[] { "bluesky", "mastodon" },
			note = "Retry"
		});

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	public async ValueTask DisposeAsync()
	{
		_client.Dispose();
		await _app.DisposeAsync();
	}
}
