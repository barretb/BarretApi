using BarretApi.Core.Models;
using Shouldly;

namespace BarretApi.Core.UnitTests.Models;

public sealed class AvatarStyle_Tests
{
    [Fact]
    public void AllContains31Styles()
    {
        AvatarStyle.All.Count.ShouldBe(31);
    }

    [Fact]
    public void AllContainsExpectedStyles()
    {
        AvatarStyle.All.ShouldContain("pixel-art");
        AvatarStyle.All.ShouldContain("adventurer");
        AvatarStyle.All.ShouldContain("bottts");
        AvatarStyle.All.ShouldContain("identicon");
        AvatarStyle.All.ShouldContain("rings");
        AvatarStyle.All.ShouldContain("toon-head");
    }

    [Theory]
    [InlineData("pixel-art")]
    [InlineData("adventurer")]
    [InlineData("bottts")]
    public void IsValidReturnsTrue_GivenValidStyle(string style)
    {
        AvatarStyle.IsValid(style).ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-a-style")]
    [InlineData(null)]
    [InlineData("")]
    public void IsValidReturnsFalse_GivenInvalidStyle(string? style)
    {
        AvatarStyle.IsValid(style).ShouldBeFalse();
    }

    [Fact]
    public void GetRandomReturnsValidStyle()
    {
        var style = AvatarStyle.GetRandom();

        AvatarStyle.IsValid(style).ShouldBeTrue();
    }
}
