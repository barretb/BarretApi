var builder = DistributedApplication.CreateBuilder(args);

var blueskyHandle = builder.AddParameter("bluesky-handle", secret: true);
var blueskyAppPassword = builder.AddParameter("bluesky-app-password", secret: true);
var mastodonInstanceUrl = builder.AddParameter("mastodon-instance-url");
var mastodonAccessToken = builder.AddParameter("mastodon-access-token", secret: true);
var authApiKey = builder.AddParameter("auth-api-key", secret: true);

builder.AddProject<Projects.BarretApi_Api>("api")
    .WithEnvironment("Bluesky__Handle", blueskyHandle)
    .WithEnvironment("Bluesky__AppPassword", blueskyAppPassword)
    .WithEnvironment("Bluesky__ServiceUrl", "https://bsky.social")
    .WithEnvironment("Mastodon__InstanceUrl", mastodonInstanceUrl)
    .WithEnvironment("Mastodon__AccessToken", mastodonAccessToken)
    .WithEnvironment("Auth__ApiKey", authApiKey);

builder.Build().Run();
