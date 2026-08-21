using BarretApi.Core.Services.Jobs;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class JobArguments_Tests
{
    private sealed record SampleArguments(string? Category, int? MaxCount, string[]? Platforms);

    [Fact]
    public void Deserialize_ReturnsNull_GivenNullOrWhitespace()
    {
        JobArguments.Deserialize<SampleArguments>(null).ShouldBeNull();
        JobArguments.Deserialize<SampleArguments>("   ").ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ReadsCamelCaseProperties()
    {
        var arguments = JobArguments.Deserialize<SampleArguments>(
            """{"category":"dotnet","maxCount":25,"platforms":["bluesky"]}""");

        arguments!.Category.ShouldBe("dotnet");
        arguments.MaxCount.ShouldBe(25);
        arguments.Platforms.ShouldBe(["bluesky"]);
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive()
    {
        var arguments = JobArguments.Deserialize<SampleArguments>("""{"Category":"dotnet"}""");

        arguments!.Category.ShouldBe("dotnet");
    }

    [Fact]
    public void Deserialize_LeavesAbsentPropertiesNull()
    {
        var arguments = JobArguments.Deserialize<SampleArguments>("""{"category":"dotnet"}""");

        arguments!.MaxCount.ShouldBeNull();
        arguments.Platforms.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_Throws_GivenMalformedJson()
    {
        Should.Throw<Exception>(() => JobArguments.Deserialize<SampleArguments>("{not json"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("""{"category":"dotnet"}""")]
    public void IsValidJson_ReturnsTrue_GivenAbsentOrWellFormedJson(string? json)
    {
        JobArguments.IsValidJson(json).ShouldBeTrue();
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[1,2")]
    [InlineData("\"unterminated")]
    public void IsValidJson_ReturnsFalse_GivenMalformedJson(string json)
    {
        JobArguments.IsValidJson(json).ShouldBeFalse();
    }

    [Fact]
    public void IsValidJson_ReturnsFalse_GivenAJsonScalar()
    {
        JobArguments.IsValidJson("42").ShouldBeFalse();
    }
}
