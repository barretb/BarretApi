using BarretApi.Api.Features.Avatar;
using BarretApi.Core.Models;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Avatar;

public sealed class GenerateAvatarValidator_Style_Tests
{
    private readonly GenerateAvatarValidator _validator = new();

    [Fact]
    public async Task ReturnsNoError_GivenNullStyle()
    {
        var request = new GenerateAvatarRequest { Style = null };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Style");
    }

    [Theory]
    [InlineData("pixel-art")]
    [InlineData("adventurer")]
    [InlineData("bottts")]
    [InlineData("identicon")]
    [InlineData("rings")]
    public async Task ReturnsNoError_GivenValidStyle(string style)
    {
        var request = new GenerateAvatarRequest { Style = style };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Style");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-a-style")]
    [InlineData("unknown-style")]
    public async Task ReturnsError_GivenInvalidStyle(string style)
    {
        var request = new GenerateAvatarRequest { Style = style };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Style");
    }

    [Fact]
    public async Task ErrorMessageContainsValidStyles_GivenInvalidStyle()
    {
        var request = new GenerateAvatarRequest { Style = "invalid-style" };

        var result = await _validator.ValidateAsync(request);

        var error = result.Errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldContain("pixel-art");
        error.ErrorMessage.ShouldContain("adventurer");
    }
}

public sealed class GenerateAvatarValidator_Format_Tests
{
    private readonly GenerateAvatarValidator _validator = new();

    [Fact]
    public async Task ReturnsNoError_GivenNullFormat()
    {
        var request = new GenerateAvatarRequest { Format = null };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Format");
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    [InlineData("avif")]
    public async Task ReturnsNoError_GivenValidFormat(string format)
    {
        var request = new GenerateAvatarRequest { Format = format };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Format");
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    public async Task ReturnsError_GivenInvalidFormat(string format)
    {
        var request = new GenerateAvatarRequest { Format = format };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Format");
    }

    [Fact]
    public async Task ErrorMessageContainsValidFormats_GivenInvalidFormat()
    {
        var request = new GenerateAvatarRequest { Format = "gif" };

        var result = await _validator.ValidateAsync(request);

        var error = result.Errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldContain("svg");
        error.ErrorMessage.ShouldContain("png");
    }
}

public sealed class GenerateAvatarValidator_Seed_Tests
{
    private readonly GenerateAvatarValidator _validator = new();

    [Fact]
    public async Task ReturnsNoError_GivenNullSeed()
    {
        var request = new GenerateAvatarRequest { Seed = null };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Seed");
    }

    [Fact]
    public async Task ReturnsNoError_GivenSeedWithin256Characters()
    {
        var request = new GenerateAvatarRequest { Seed = new string('a', 256) };

        var result = await _validator.ValidateAsync(request);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Seed");
    }

    [Fact]
    public async Task ReturnsError_GivenSeedExceeding256Characters()
    {
        var request = new GenerateAvatarRequest { Seed = new string('a', 257) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Seed");
    }
}
