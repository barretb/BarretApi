var builder = DistributedApplication.CreateBuilder(args);

var blueskyHandle = builder.AddParameter("bluesky-handle", secret: true);
var blueskyAppPassword = builder.AddParameter("bluesky-app-password", secret: true);
var linkedInClientId = builder.AddParameter("linkedin-client-id", secret: true);
var linkedInClientSecret = builder.AddParameter("linkedin-client-secret", secret: true);
var linkedInAuthorUrn = builder.AddParameter("linkedin-author-urn");
var linkedInApiBaseUrl = builder.AddParameter("linkedin-api-base-url");
var linkedInOAuthBaseUrl = builder.AddParameter("linkedin-oauth-base-url");
var linkedInTokenStorageTableName = builder.AddParameter("linkedin-token-storage-table-name");
var mastodonInstanceUrl = builder.AddParameter("mastodon-instance-url");
var mastodonAccessToken = builder.AddParameter("mastodon-access-token", secret: true);
var authApiKey = builder.AddParameter("auth-api-key", secret: true);
var blogPromotionFeedUrl = builder.AddParameter("blog-promotion-feed-url");
var blogPromotionRecentDaysWindow = builder.AddParameter("blog-promotion-recent-days-window");
var blogPromotionEnableReminderPosts = builder.AddParameter("blog-promotion-enable-reminder-posts");
var blogPromotionReminderDelayHours = builder.AddParameter("blog-promotion-reminder-delay-hours");
var blogPromotionTableStorageTableName = builder.AddParameter("blog-promotion-table-storage-table-name");
var blogPromotionTableStoragePartitionKey = builder.AddParameter("blog-promotion-table-storage-partition-key");

var azurite = builder.AddContainer("azurite", "mcr.microsoft.com/azure-storage/azurite")
    .WithArgs("azurite", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0")
    .WithEndpoint(name: "blob", port: 10000, targetPort: 10000)
    .WithEndpoint(name: "queue", port: 10001, targetPort: 10001)
    .WithEndpoint(name: "table", port: 10002, targetPort: 10002)
    .WithVolume("azurite-data", "/data");

const string azuriteConnectionString = "UseDevelopmentStorage=true";

builder.AddProject<Projects.BarretApi_Api>("api")
    .WaitFor(azurite)
    .WithEnvironment("Bluesky__Handle", blueskyHandle)
    .WithEnvironment("Bluesky__AppPassword", blueskyAppPassword)
    .WithEnvironment("Bluesky__ServiceUrl", "https://bsky.social")
    .WithEnvironment("LinkedIn__ClientId", linkedInClientId)
    .WithEnvironment("LinkedIn__ClientSecret", linkedInClientSecret)
    .WithEnvironment("LinkedIn__AuthorUrn", linkedInAuthorUrn)
    .WithEnvironment("LinkedIn__ApiBaseUrl", linkedInApiBaseUrl)
    .WithEnvironment("LinkedIn__OAuthBaseUrl", linkedInOAuthBaseUrl)
    .WithEnvironment("LinkedIn__TokenStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("LinkedIn__TokenStorage__TableName", linkedInTokenStorageTableName)
    .WithEnvironment("Mastodon__InstanceUrl", mastodonInstanceUrl)
    .WithEnvironment("Mastodon__AccessToken", mastodonAccessToken)
    .WithEnvironment("Auth__ApiKey", authApiKey)
    .WithEnvironment("BlogPromotion__FeedUrl", blogPromotionFeedUrl)
    .WithEnvironment("BlogPromotion__RecentDaysWindow", blogPromotionRecentDaysWindow)
    .WithEnvironment("BlogPromotion__EnableReminderPosts", blogPromotionEnableReminderPosts)
    .WithEnvironment("BlogPromotion__ReminderDelayHours", blogPromotionReminderDelayHours)
    .WithEnvironment("BlogPromotion__TableStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("BlogPromotion__TableStorage__TableName", blogPromotionTableStorageTableName)
    .WithEnvironment("BlogPromotion__TableStorage__PartitionKey", blogPromotionTableStoragePartitionKey);

builder.Build().Run();
