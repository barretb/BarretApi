using BarretApi.Api.Auth;
using BarretApi.Api.Validation;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Services;
using BarretApi.Infrastructure.Bluesky;
using BarretApi.Infrastructure.LinkedIn;
using BarretApi.Infrastructure.Mastodon;
using BarretApi.Infrastructure.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Temporary startup diagnostics (remove after debugging) ---
Console.WriteLine("=== CONFIGURATION DIAGNOSTICS ===");
Console.WriteLine($"LinkedIn:ClientId = '{builder.Configuration["LinkedIn:ClientId"]}'");
Console.WriteLine($"LinkedIn:ClientSecret length = {builder.Configuration["LinkedIn:ClientSecret"]?.Length ?? 0}");
Console.WriteLine($"LinkedIn:AuthorUrn = '{builder.Configuration["LinkedIn:AuthorUrn"]}'");
Console.WriteLine($"LinkedIn:TokenStorage:TableName = '{builder.Configuration["LinkedIn:TokenStorage:TableName"]}'");
Console.WriteLine($"LinkedIn:TokenStorage:ConnectionString length = {builder.Configuration["LinkedIn:TokenStorage:ConnectionString"]?.Length ?? 0}");
Console.WriteLine($"LinkedIn:TokenStorage:AccountEndpoint = '{builder.Configuration["LinkedIn:TokenStorage:AccountEndpoint"]}'");
Console.WriteLine("Environment variables containing 'LinkedIn' (case-insensitive):");
foreach (var kvp in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
	.Where(e => e.Key.ToString()!.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase)))
{
	Console.WriteLine($"  {kvp.Key} = (length: {kvp.Value?.ToString()?.Length ?? 0})");
}
Console.WriteLine("=== END DIAGNOSTICS ===");
// --- End temporary diagnostics ---

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
builder.Services.AddSingleton<IBlogPromotionOrchestrator, BlogPromotionOrchestrator>();
builder.Services.AddSingleton<SocialPostService>();

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
