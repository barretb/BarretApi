using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableScheduledSocialPostRepository_Mapping_Tests
{
	[Fact]
	public void PreservesThreadingAndDeliveryReceipts_GivenStoredRecord()
	{
		var now = DateTimeOffset.UtcNow;
		var record = new ScheduledSocialPostRecord
		{
			ScheduledPostId = "post",
			ScheduledForUtc = now,
			CreatedAtUtc = now.AddHours(-1),
			Status = ScheduledPostStatus.Processing,
			Text = "thread",
			AutoThread = true,
			TargetPlatforms = ["bluesky"],
			DeliveryTrackingEnabled = true,
			LeaseExpiresAtUtc = now.AddMinutes(15),
			DeliveryResults = [new PlatformPostResult
			{
				Platform = "bluesky", Success = false, ErrorCode = "RATE_LIMITED",
				ThreadResults = [new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "root", PostUrl = "https://example.com/post" }]
			}]
		};

		var entity = AzureTableScheduledSocialPostRepository.MapModelToEntity(record, "scheduled");
		entity.ETag = new ETag("version-1");
		var restored = AzureTableScheduledSocialPostRepository.MapEntityToModel(entity);

		restored.AutoThread.ShouldBeTrue();
		restored.DeliveryTrackingEnabled.ShouldBeTrue();
		restored.LeaseExpiresAtUtc.ShouldBe(record.LeaseExpiresAtUtc);
		restored.Version.ShouldBe("version-1");
		restored.TargetPlatforms.ShouldBe(record.TargetPlatforms);
		restored.DeliveryResults.Single().ThreadResults!.Single().PostId.ShouldBe("root");
	}

	[Fact]
	public void UsesSafeDefaults_GivenLegacyEntity()
	{
		var entity = new TableEntity("scheduled", "old") { ["Status"] = "Failed" };

		var record = AzureTableScheduledSocialPostRepository.MapEntityToModel(entity);

		record.AutoThread.ShouldBeFalse();
		record.DeliveryTrackingEnabled.ShouldBeFalse();
		record.DeliveryResults.ShouldBeEmpty();
		record.LeaseExpiresAtUtc.ShouldBeNull();
		record.Status.ShouldBe(ScheduledPostStatus.Failed);
	}
}
