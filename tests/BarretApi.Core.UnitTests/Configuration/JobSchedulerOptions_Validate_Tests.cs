using BarretApi.Core.Configuration;
using Shouldly;

namespace BarretApi.Core.UnitTests.Configuration;

public sealed class JobSchedulerOptions_Validate_Tests
{
	private static JobSchedulerOptions CreateValid() => new()
	{
		TableStorage = new JobSchedulerTableStorageOptions
		{
			ConnectionString = "UseDevelopmentStorage=true"
		}
	};

	[Fact]
	public void ReturnsNull_GivenValidOptions()
	{
		var options = CreateValid();

		options.Validate().ShouldBeNull();
	}

	[Fact]
	public void DefaultsSchedulerToDisabled()
	{
		var options = CreateValid();

		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void DefaultsMatchTheSpec()
	{
		var options = CreateValid();

		options.TickIntervalSeconds.ShouldBe(30);
		options.ClaimTimeoutMinutes.ShouldBe(30);
		options.RunRetentionDays.ShouldBe(30);
		options.TableStorage.JobsTableName.ShouldBe("scheduledjobs");
		options.TableStorage.RunsTableName.ShouldBe("jobruns");
		options.TableStorage.PartitionKey.ShouldBe("scheduled-job");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(3_601)]
	public void ReturnsError_GivenTickIntervalOutOfRange(int seconds)
	{
		var options = new JobSchedulerOptions
		{
			TickIntervalSeconds = seconds,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate()!.ShouldContain("TickIntervalSeconds");
	}

	[Fact]
	public void ReturnsError_GivenClaimTimeoutNotPositive()
	{
		var options = new JobSchedulerOptions
		{
			ClaimTimeoutMinutes = 0,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate()!.ShouldContain("ClaimTimeoutMinutes");
	}

	[Fact]
	public void ReturnsError_GivenRetentionDaysNotPositive()
	{
		var options = new JobSchedulerOptions
		{
			RunRetentionDays = 0,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate()!.ShouldContain("RunRetentionDays");
	}

	[Fact]
	public void ReturnsError_GivenNeitherConnectionStringNorAccountEndpoint()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions()
		};

		options.Validate()!.ShouldContain("ConnectionString or AccountEndpoint");
	}

	[Fact]
	public void ReturnsError_GivenAccountEndpointIsNotHttps()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				AccountEndpoint = "http://example.table.core.windows.net"
			}
		};

		options.Validate()!.ShouldContain("https");
	}

	[Theory]
	[InlineData("ab")]
	[InlineData("1jobs")]
	[InlineData("job-runs")]
	public void ReturnsError_GivenInvalidRunsTableName(string tableName)
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				RunsTableName = tableName
			}
		};

		options.Validate()!.ShouldContain("RunsTableName");
	}

	[Fact]
	public void ReturnsError_GivenEmptyPartitionKey()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				PartitionKey = "   "
			}
		};

		options.Validate()!.ShouldContain("PartitionKey");
	}

	[Fact]
	public void ThrowIfInvalid_ThrowsWithTheValidationMessage()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions()
		};

		Should.Throw<InvalidOperationException>(options.ThrowIfInvalid)
			.Message.ShouldContain("ConnectionString or AccountEndpoint");
	}
}
