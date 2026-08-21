using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobHandlerRegistry_Tests
{
    private sealed class StubHandler(string jobType) : IScheduledJobHandler
    {
        public string JobType { get; } = jobType;

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JobExecutionResult.Ok("stub"));
    }

    [Fact]
    public void Resolve_ReturnsTheHandler_GivenARegisteredType()
    {
        var handler = new StubHandler("tip-of-day");
        var registry = new JobHandlerRegistry([handler]);

        registry.Resolve("tip-of-day").ShouldBeSameAs(handler);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var handler = new StubHandler("tip-of-day");
        var registry = new JobHandlerRegistry([handler]);

        registry.Resolve("Tip-Of-Day").ShouldBeSameAs(handler);
    }

    [Fact]
    public void Resolve_Throws_GivenAnUnregisteredType()
    {
        var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

        Should.Throw<InvalidOperationException>(() => registry.Resolve("nope"))
            .Message.ShouldContain("nope");
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_GivenAnUnregisteredType()
    {
        var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

        registry.IsRegistered("nope").ShouldBeFalse();
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_GivenNullOrWhitespace()
    {
        var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

        registry.IsRegistered("   ").ShouldBeFalse();
    }

    [Fact]
    public void Constructor_Throws_GivenDuplicateJobTypes()
    {
        Should.Throw<InvalidOperationException>(() =>
                new JobHandlerRegistry([new StubHandler("tip-of-day"), new StubHandler("TIP-OF-DAY")]))
            .Message.ShouldContain("tip-of-day");
    }

    [Fact]
    public void Constructor_Throws_GivenAHandlerWithABlankJobType()
    {
        Should.Throw<InvalidOperationException>(() => new JobHandlerRegistry([new StubHandler("  ")]));
    }

    [Fact]
    public void RegisteredTypes_ReturnsEveryTypeSorted()
    {
        var registry = new JobHandlerRegistry(
            [new StubHandler("tip-of-day"), new StubHandler("nasa-apod")]);

        registry.RegisteredTypes.ShouldBe(["nasa-apod", "tip-of-day"]);
    }
}
