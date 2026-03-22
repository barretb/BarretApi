using BarretApi.Api.Auth;
using BarretApi.Api.Validation;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Services;
using BarretApi.Infrastructure.Bluesky;
using BarretApi.Infrastructure.DiceBear;
using BarretApi.Infrastructure.LinkedIn;
using BarretApi.Infrastructure.Mastodon;
using BarretApi.Infrastructure.Nasa;
using BarretApi.Infrastructure.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.Configure<BlueskyOptions>(builder.Configuration.GetSection(BlueskyOptions.SectionName));
builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection(MastodonOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services
    .AddOptions<LinkedInOptions>()
    .Bind(builder.Configuration.GetSection(LinkedInOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LinkedInOptions>>(
    new OptionsValidatorAdapter<LinkedInOptions>(o => o.Validate()));
builder.Services
    .AddOptions<BlogPromotionOptions>()
    .Bind(builder.Configuration.GetSection(BlogPromotionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BlogPromotionOptions>>(
    new OptionsValidatorAdapter<BlogPromotionOptions>(o => o.Validate()));
builder.Services
    .AddOptions<ScheduledSocialPostOptions>()
    .Bind(builder.Configuration.GetSection(ScheduledSocialPostOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ScheduledSocialPostOptions>>(
    new OptionsValidatorAdapter<ScheduledSocialPostOptions>(o => o.Validate()));

builder.Services
    .AddAuthentication(ApiKeyAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);

builder.Services.AddAuthorization();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "BarretApi";
        settings.Version = "v1";
    };
});

builder.Services.AddHttpClient<BlueskyClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Bluesky:ServiceUrl"] ?? "https://bsky.social");
});

builder.Services.AddHttpClient<MastodonClient>((sp, client) =>
{
    var mastodonOptions = builder.Configuration.GetSection(MastodonOptions.SectionName);
    client.BaseAddress = new Uri(mastodonOptions["InstanceUrl"] ?? "https://mastodon.social");
});

builder.Services.AddHttpClient<LinkedInClient>((sp, client) =>
{
    var linkedInOptions = builder.Configuration.GetSection(LinkedInOptions.SectionName);
    client.BaseAddress = new Uri(linkedInOptions["ApiBaseUrl"] ?? "https://api.linkedin.com");
});

builder.Services.AddHttpClient<LinkedInTokenProvider>((sp, client) =>
{
    var linkedInOptions = builder.Configuration.GetSection(LinkedInOptions.SectionName);
    client.BaseAddress = new Uri(linkedInOptions["OAuthBaseUrl"] ?? "https://www.linkedin.com");
});

builder.Services.AddHttpClient("LinkedInOAuth", (sp, client) =>
{
    var linkedInOptions = builder.Configuration.GetSection(LinkedInOptions.SectionName);
    client.BaseAddress = new Uri(linkedInOptions["OAuthBaseUrl"] ?? "https://www.linkedin.com");
});

builder.Services.AddSingleton<ILinkedInTokenStore, AzureTableLinkedInTokenStore>();

builder.Services.AddHttpClient<ImageDownloadService>();
builder.Services.AddHttpClient<IBlogFeedReader, RssBlogFeedReader>();

builder.Services.AddSingleton<ISocialPlatformClient>(sp =>
    sp.GetRequiredService<BlueskyClient>());
builder.Services.AddSingleton<ISocialPlatformClient>(sp =>
    sp.GetRequiredService<MastodonClient>());
builder.Services.AddSingleton<ISocialPlatformClient>(sp =>
    sp.GetRequiredService<LinkedInClient>());
builder.Services.AddSingleton<ITextShorteningService, TextShorteningService>();
builder.Services.AddSingleton<IHashtagService, HashtagService>();
builder.Services.AddSingleton<IImageDownloadService>(sp =>
    sp.GetRequiredService<ImageDownloadService>());
builder.Services.AddSingleton<IBlogPostPromotionRepository, AzureTableBlogPostPromotionRepository>();
builder.Services.AddSingleton<IScheduledSocialPostRepository, AzureTableScheduledSocialPostRepository>();
builder.Services.AddSingleton<IBlogPromotionOrchestrator, BlogPromotionOrchestrator>();
builder.Services.AddSingleton<IScheduledSocialPostProcessor, ScheduledSocialPostProcessor>();
builder.Services.AddSingleton<SocialPostService>();
builder.Services.AddSingleton<RssRandomPostService>();

builder.Services.Configure<NasaApodOptions>(builder.Configuration.GetSection(NasaApodOptions.SectionName));
builder.Services.AddHttpClient<NasaApodClient>((sp, client) =>
{
    var nasaOptions = builder.Configuration.GetSection(NasaApodOptions.SectionName);
    client.BaseAddress = new Uri(nasaOptions["BaseUrl"] ?? "https://api.nasa.gov");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<INasaApodClient>(sp => sp.GetRequiredService<NasaApodClient>());
builder.Services.AddSingleton<IImageResizer, SkiaImageResizer>();
builder.Services.AddSingleton<NasaApodPostService>();

builder.Services.Configure<NasaGibsOptions>(builder.Configuration.GetSection(NasaGibsOptions.SectionName));
builder.Services.AddHttpClient<NasaGibsClient>((sp, client) =>
{
    var gibsOptions = builder.Configuration.GetSection(NasaGibsOptions.SectionName);
    client.BaseAddress = new Uri(
        gibsOptions["BaseUrl"] ?? "https://wvs.earthdata.nasa.gov");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<INasaGibsClient>(sp => sp.GetRequiredService<NasaGibsClient>());
builder.Services.AddSingleton<NasaGibsPostService>();

builder.Services.AddHttpClient<DiceBearAvatarClient>(client =>
{
    client.BaseAddress = new Uri("https://api.dicebear.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<IDiceBearAvatarClient>(sp =>
    sp.GetRequiredService<DiceBearAvatarClient>());

builder.Services.AddHttpClient<AngleSharpHtmlTextExtractor>();
builder.Services.AddSingleton<IHtmlTextExtractor>(sp =>
    sp.GetRequiredService<AngleSharpHtmlTextExtractor>());
builder.Services.AddSingleton<IWordCloudGenerator, SkiaWordCloudGenerator>();
builder.Services.AddSingleton<TextAnalysisService>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapDefaultEndpoints();

app.Run();
