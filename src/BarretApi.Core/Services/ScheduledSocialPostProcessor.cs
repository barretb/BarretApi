using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

public sealed class ScheduledSocialPostProcessor(
    IScheduledSocialPostRepository scheduledSocialPostRepository,
    SocialPostService socialPostService,
    IScheduledPostImageStore scheduledPostImageStore,
    IOptions<ScheduledSocialPostOptions> options,
    ILogger<ScheduledSocialPostProcessor> logger)
    : IScheduledSocialPostProcessor
{
    private readonly IScheduledSocialPostRepository _scheduledSocialPostRepository = scheduledSocialPostRepository;
    private readonly SocialPostService _socialPostService = socialPostService;
    private readonly IScheduledPostImageStore _scheduledPostImageStore = scheduledPostImageStore;
    private readonly ScheduledSocialPostOptions _options = options.Value;
    private readonly ILogger<ScheduledSocialPostProcessor> _logger = logger;

    public async Task<ScheduledPostProcessingSummary> ProcessDueAsync(
        int? maxCount,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var runId = $"sched-run-{startedAt:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var effectiveMax = Math.Clamp(maxCount ?? _options.MaxBatchSize, 1, 1_000);

        var dueRecords = await _scheduledSocialPostRepository.GetDueForProcessingAsync(
            startedAt,
            effectiveMax,
            cancellationToken);

        var succeededCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var attemptedCount = 0;
        var failures = new List<ScheduledPostFailureDetails>();

        foreach (var dueRecord in dueRecords)
        {
            var now = DateTimeOffset.UtcNow;
            var claimed = await _scheduledSocialPostRepository.TryMarkProcessingAsync(
                dueRecord.ScheduledPostId,
                now,
                cancellationToken);
            if (!claimed)
            {
                skippedCount++;
                continue;
            }

            attemptedCount++;

            var post = await MapToSocialPostAsync(dueRecord, cancellationToken);
            var results = await _socialPostService.PostAsync(post, cancellationToken);

            if (results.Count > 0 && results.All(r => r.Success))
            {
                succeededCount++;
                await _scheduledSocialPostRepository.MarkPublishedAsync(
                    dueRecord.ScheduledPostId,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                continue;
            }

            failedCount++;
            var failedResults = results.Where(r => !r.Success).ToList();
            var partialSuccess = results.Any(r => r.Success);
            var firstFailure = failedResults.FirstOrDefault() ?? results.FirstOrDefault();
            var errorCode = partialSuccess
                ? "PARTIAL_PLATFORM_FAILURE"
                : firstFailure?.ErrorCode ?? "PUBLISH_FAILED";
            var errorMessage = partialSuccess
                ? "One or more target platforms failed while processing a scheduled post."
                : firstFailure?.ErrorMessage ?? "No platform succeeded for the scheduled post.";

            await _scheduledSocialPostRepository.MarkFailedAsync(
                dueRecord.ScheduledPostId,
                errorCode,
                errorMessage,
                DateTimeOffset.UtcNow,
                cancellationToken);

            failures.Add(new ScheduledPostFailureDetails
            {
                ScheduledPostId = dueRecord.ScheduledPostId,
                ScheduledForUtc = dueRecord.ScheduledForUtc,
                Platforms = dueRecord.TargetPlatforms,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                AttemptedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var summary = new ScheduledPostProcessingSummary
        {
            RunId = runId,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DueCount = dueRecords.Count,
            AttemptedCount = attemptedCount,
            SucceededCount = succeededCount,
            FailedCount = failedCount,
            SkippedCount = skippedCount,
            Failures = failures
        };

        _logger.LogInformation(
            "Scheduled processing run {RunId} completed: Due={DueCount}, Attempted={AttemptedCount}, Succeeded={SucceededCount}, Failed={FailedCount}, Skipped={SkippedCount}",
            summary.RunId,
            summary.DueCount,
            summary.AttemptedCount,
            summary.SucceededCount,
            summary.FailedCount,
            summary.SkippedCount);

        return summary;
    }

    private async Task<SocialPost> MapToSocialPostAsync(ScheduledSocialPostRecord record, CancellationToken cancellationToken)
    {
        var images = new List<ImageData>();
        foreach (var storedImage in record.UploadedImages)
        {
            var content = await _scheduledPostImageStore.DownloadAsync(storedImage.BlobName, cancellationToken);
            images.Add(new ImageData
            {
                Content = content,
                ContentType = storedImage.ContentType,
                AltText = storedImage.AltText,
                FileName = storedImage.FileName
            });
        }

        return new SocialPost
        {
            Text = record.Text,
            Hashtags = record.Hashtags,
            TargetPlatforms = record.TargetPlatforms,
            ImageUrls = record.ImageUrls,
            Images = images
        };
    }
}
