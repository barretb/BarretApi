using BarretApi.Core.Configuration;
using Shouldly;

namespace BarretApi.Core.UnitTests.Configuration;

public sealed class ScheduledSocialPostOptions_Validate_Tests
{
	[Fact]
	public void ReturnsError_GivenInvalidTableName()
	{
		var options = new ScheduledSocialPostOptions
		{
			TableStorage = new ScheduledSocialPostTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				TableName = "scheduled-social-post",
				PartitionKey = "scheduled-social-post"
			},
			MaxBatchSize = 100
		};

		var result = options.Validate();

		result.ShouldBe("ScheduledSocialPost:TableStorage:TableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).");
	}

	[Fact]
	public void ReturnsNull_GivenValidConnectionStringAndTableName()
	{
		var options = CreateOptions();

		var result = options.Validate();

		result.ShouldBeNull();
	}

	private static ScheduledSocialPostOptions CreateOptions()
	{
		return new ScheduledSocialPostOptions
		{
			TableStorage = new ScheduledSocialPostTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				TableName = "scheduledsocialposts",
				PartitionKey = "scheduled-social-post"
			},
			MaxBatchSize = 100
		};
	}
}