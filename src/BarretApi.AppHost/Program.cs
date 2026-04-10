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
var scheduledSocialPostMaxBatchSize = builder.AddParameter("scheduled-social-post-max-batch-size");
var scheduledSocialPostTableStorageTableName = builder.AddParameter("scheduled-social-post-table-storage-table-name");
var scheduledSocialPostTableStoragePartitionKey = builder.AddParameter("scheduled-social-post-table-storage-partition-key");
var scheduledSocialPostBlobStorageContainerName = builder.AddParameter("scheduled-social-post-blob-storage-container-name");
var gitHubClientId = builder.AddParameter("github-client-id", secret: true);
var gitHubClientSecret = builder.AddParameter("github-client-secret", secret: true);
var gitHubApiBaseUrl = builder.AddParameter("github-api-base-url");
var gitHubOAuthBaseUrl = builder.AddParameter("github-oauth-base-url");
var gitHubTokenStorageTableName = builder.AddParameter("github-token-storage-table-name");
var gitHubRepoStorageTableName = builder.AddParameter("github-repo-storage-table-name");
var nasaApodApiKey = builder.AddParameter("nasa-apod-api-key", secret: true);
var gibsBaseUrl = builder.AddParameter("gibs-base-url");
var gibsDefaultLayer = builder.AddParameter("gibs-default-layer");
var gibsBboxSouth = builder.AddParameter("gibs-bbox-south");
var gibsBboxWest = builder.AddParameter("gibs-bbox-west");
var gibsBboxNorth = builder.AddParameter("gibs-bbox-north");
var gibsBboxEast = builder.AddParameter("gibs-bbox-east");
var gibsImageWidth = builder.AddParameter("gibs-image-width");
var gibsImageHeight = builder.AddParameter("gibs-image-height");

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
    .WithEnvironment("BlogPromotion__TableStorage__PartitionKey", blogPromotionTableStoragePartitionKey)
    .WithEnvironment("ScheduledSocialPost__MaxBatchSize", scheduledSocialPostMaxBatchSize)
    .WithEnvironment("ScheduledSocialPost__TableStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("ScheduledSocialPost__TableStorage__TableName", scheduledSocialPostTableStorageTableName)
    .WithEnvironment("ScheduledSocialPost__TableStorage__PartitionKey", scheduledSocialPostTableStoragePartitionKey)
    .WithEnvironment("ScheduledSocialPost__BlobStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("ScheduledSocialPost__BlobStorage__ContainerName", scheduledSocialPostBlobStorageContainerName)
    .WithEnvironment("NasaApod__ApiKey", nasaApodApiKey)
    .WithEnvironment("NasaGibs__BaseUrl", gibsBaseUrl)
    .WithEnvironment("NasaGibs__DefaultLayer", gibsDefaultLayer)
    .WithEnvironment("NasaGibs__BboxSouth", gibsBboxSouth)
    .WithEnvironment("NasaGibs__BboxWest", gibsBboxWest)
    .WithEnvironment("NasaGibs__BboxNorth", gibsBboxNorth)
    .WithEnvironment("NasaGibs__BboxEast", gibsBboxEast)
    .WithEnvironment("NasaGibs__ImageWidth", gibsImageWidth)
    .WithEnvironment("NasaGibs__ImageHeight", gibsImageHeight)
    .WithEnvironment("GitHub__ClientId", gitHubClientId)
    .WithEnvironment("GitHub__ClientSecret", gitHubClientSecret)
    .WithEnvironment("GitHub__ApiBaseUrl", gitHubApiBaseUrl)
    .WithEnvironment("GitHub__OAuthBaseUrl", gitHubOAuthBaseUrl)
    .WithEnvironment("GitHub__TokenStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("GitHub__TokenStorage__TableName", gitHubTokenStorageTableName)
    .WithEnvironment("GitHub__RepoStorage__ConnectionString", azuriteConnectionString)
    .WithEnvironment("GitHub__RepoStorage__TableName", gitHubRepoStorageTableName);

builder.Build().Run();
