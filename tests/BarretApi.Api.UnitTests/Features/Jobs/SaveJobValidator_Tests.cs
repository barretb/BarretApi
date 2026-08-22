using BarretApi.Api.Features.Jobs;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class SaveJobValidator_Tests
{
    private readonly SaveJobValidator _sut = new();

    private static SaveJobRequest CreateValid()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "America/Chicago",
            ArgumentsJson = """{"category":"dotnet"}""",
            IsEnabled = true,
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30
        };

    [Fact]
    public void Passes_GivenAValidRequest()
    {
        _sut.Validate(CreateValid()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Daily Tip")]
    [InlineData("daily_tip")]
    [InlineData("-daily")]
    [InlineData("daily-")]
    public void Fails_GivenANameThatIsNotASlug(string name)
    {
        var request = CreateValid();
        request.Name = name;

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Fails_GivenANameLongerThan100Characters()
    {
        var request = CreateValid();
        request.Name = new string('a', 101);

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Fails_GivenABlankJobType()
    {
        var request = CreateValid();
        request.JobType = "  ";

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Fails_GivenABlankCronExpression()
    {
        var request = CreateValid();
        request.CronExpression = string.Empty;

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Fails_GivenMaxRetryCountOutOfRange(int maxRetryCount)
    {
        var request = CreateValid();
        request.MaxRetryCount = maxRetryCount;

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3_601)]
    public void Fails_GivenRetryBaseDelayOutOfRange(int seconds)
    {
        var request = CreateValid();
        request.RetryBaseDelaySeconds = seconds;

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Fails_GivenMalformedArgumentsJson()
    {
        var request = CreateValid();
        request.ArgumentsJson = "{not json";

        _sut.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Passes_GivenNoArgumentsJson()
    {
        var request = CreateValid();
        request.ArgumentsJson = null;

        _sut.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Passes_GivenNoDisplayName()
    {
        var request = CreateValid();
        request.DisplayName = null;

        _sut.Validate(request).IsValid.ShouldBeTrue();
    }
}
