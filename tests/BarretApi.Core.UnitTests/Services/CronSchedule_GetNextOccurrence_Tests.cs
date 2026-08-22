using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class CronSchedule_GetNextOccurrence_Tests
{
    private static CronSchedule Parse(string expression, string timeZoneId)
    {
        CronSchedule.TryParse(expression, timeZoneId, out var schedule, out var error).ShouldBeTrue(error);
        return schedule!;
    }

    [Fact]
    public void ReturnsNextDailyOccurrence_GivenUtcSchedule()
    {
        var schedule = Parse("0 8 * * *", "UTC");

        var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));

        next.ShouldBe(new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ResolvesAgainstTheJobTimeZone_NotUtc()
    {
        var schedule = Parse("0 8 * * *", "America/Chicago");

        var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        next.ShouldBe(new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AcceptsSixFieldExpressionsWithSeconds()
    {
        var schedule = Parse("30 0 8 * * *", "UTC");

        var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        next.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 30, TimeSpan.Zero));
    }

    [Fact]
    public void FiresOnce_GivenSpringForwardTransition()
    {
        var schedule = Parse("0 2 * * *", "America/Chicago");

        var first = schedule.GetNextOccurrence(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.Zero));
        var second = schedule.GetNextOccurrence(first!.Value);

        first.ShouldBe(new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero));
        second.ShouldBe(new DateTimeOffset(2026, 3, 9, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FiresOnce_GivenFallBackTransition()
    {
        var schedule = Parse("0 1 * * *", "America/Chicago");

        var first = schedule.GetNextOccurrence(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        var second = schedule.GetNextOccurrence(first!.Value);

        first.ShouldBe(new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero));
        second.ShouldBe(new DateTimeOffset(2026, 11, 2, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ReturnsUtcOffset_ForEveryOccurrence()
    {
        var schedule = Parse("0 8 * * *", "America/Chicago");

        var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        next!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("* * * *")]
    [InlineData("99 * * * *")]
    public void TryParse_ReturnsFalseWithAnError_GivenAnInvalidExpression(string expression)
    {
        var parsed = CronSchedule.TryParse(expression, "UTC", out var schedule, out var error);

        parsed.ShouldBeFalse();
        schedule.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParse_ReturnsFalseWithAnError_GivenAnUnknownTimeZone()
    {
        var parsed = CronSchedule.TryParse("0 8 * * *", "Mars/Olympus_Mons", out var schedule, out var error);

        parsed.ShouldBeFalse();
        schedule.ShouldBeNull();
        error!.ShouldContain("time zone");
    }

    [Fact]
    public void TryParse_TreatsEmptyTimeZoneAsUtc()
    {
        var parsed = CronSchedule.TryParse("0 8 * * *", string.Empty, out var schedule, out var error);

        parsed.ShouldBeTrue(error);
        schedule!.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
    }
}
