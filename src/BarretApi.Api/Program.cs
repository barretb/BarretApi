using BarretApi.Api.Auth;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Services;
using BarretApi.Infrastructure.Bluesky;
using BarretApi.Infrastructure.Mastodon;
using BarretApi.Infrastructure.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;

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

builder.Services.AddHttpClient<ImageDownloadService>();

builder.Services.AddSingleton<ISocialPlatformClient>(sp =>
    sp.GetRequiredService<BlueskyClient>());
builder.Services.AddSingleton<ISocialPlatformClient>(sp =>
    sp.GetRequiredService<MastodonClient>());
builder.Services.AddSingleton<ITextShorteningService, TextShorteningService>();
builder.Services.AddSingleton<IHashtagService, HashtagService>();
builder.Services.AddSingleton<IImageDownloadService>(sp =>
    sp.GetRequiredService<ImageDownloadService>());
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
