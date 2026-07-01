using Azure.Data.Tables;
using BarretApi.Infrastructure.Services;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableTipOfDayRepository_MapEntityToModel_Tests
{
	[Fact]
	public void ReadsCamelCaseUploadFields_GivenImportedEntity()
	{
		var entity = new TableEntity("tip-of-the-day", "tip-1")
		{
			["category"] = "dotnet",
			["tip"] = "Prefer file-scoped namespaces.",
			["url"] = "https://example.com/tip"
		};

		var record = AzureTableTipOfDayRepository.MapEntityToModel(entity);

		record.TipId.ShouldBe("tip-1");
		record.Category.ShouldBe("dotnet");
		record.Tip.ShouldBe("Prefer file-scoped namespaces.");
		record.MoreInfoUrl.ShouldBe("https://example.com/tip");
		record.LastPostedDate.ShouldBeNull();
	}

	[Fact]
	public void DoesNotUseSystemTimestampAsLastPostedDate_GivenNoExplicitLastPostedDate()
	{
		var entity = new TableEntity("tip-of-the-day", "tip-1")
		{
			["category"] = "dotnet",
			["tip"] = "Prefer file-scoped namespaces."
		};

		var record = AzureTableTipOfDayRepository.MapEntityToModel(entity);

		record.LastPostedDate.ShouldBeNull();
	}

	[Fact]
	public void PrefersCamelCaseLastPostedDate_GivenPascalAndCamelCaseDates()
	{
		var ignored = DateTimeOffset.UtcNow.AddDays(-200);
		var expected = DateTimeOffset.UtcNow;
		var entity = new TableEntity("tip-of-the-day", "tip-1")
		{
			["Category"] = "dotnet",
			["Tip"] = "Prefer file-scoped namespaces.",
			["LastPostedDate"] = expected,
			["lastPostedDate"] = ignored
		};

		var record = AzureTableTipOfDayRepository.MapEntityToModel(entity);

		record.LastPostedDate.ShouldBe(expected);
	}
}
