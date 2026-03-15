using BarretApi.Api.Features.Nasa;
using BarretApi.Core.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Nasa;

public sealed class SatellitePostValidator_Tests
{
    private readonly SatellitePostValidator _validator;

    public SatellitePostValidator_Tests()
    {
        var options = Substitute.For<IOptions<NasaGibsOptions>>();
        options.Value.Returns(new NasaGibsOptions());
        _validator = new SatellitePostValidator(options);
    }

    // --- Platforms validation ---

    [Fact]
    public void IsValid_GivenEmptyRequest()
    {
        var req = new SatellitePostRequest();

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_GivenNullPlatforms()
    {
        var req = new SatellitePostRequest { Platforms = null };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_GivenValidPlatforms()
    {
        var req = new SatellitePostRequest { Platforms = ["bluesky", "mastodon", "linkedin"] };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_GivenCaseInsensitivePlatforms()
    {
        var req = new SatellitePostRequest { Platforms = ["Bluesky", "MASTODON", "LinkedIn"] };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RejectsPlatform_GivenInvalidPlatformName()
    {
        var req = new SatellitePostRequest { Platforms = ["twitter"] };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
    }

    [Fact]
    public void RejectsPlatforms_GivenMixedValidAndInvalidPlatforms()
    {
        var req = new SatellitePostRequest { Platforms = ["bluesky", "twitter"] };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
    }

    [Fact]
    public void IsValid_GivenEmptyPlatformsList()
    {
        var req = new SatellitePostRequest { Platforms = [] };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeTrue();
    }

    // --- Date validation (US2) ---

    [Fact]
    public void RejectsDate_GivenInvalidDateFormat()
    {
        var req = new SatellitePostRequest { Date = "not-a-date" };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void RejectsDate_GivenFutureDate()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var req = new SatellitePostRequest { Date = tomorrow.ToString("yyyy-MM-dd") };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void RejectsDate_GivenDateBeforeModisTerraStart()
    {
        var req = new SatellitePostRequest { Date = "2000-02-23" };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date" && e.ErrorMessage.Contains("2000-02-24"));
    }

    [Fact]
    public void RejectsDate_GivenDateBeforeViirsSNOAA21Start()
    {
        var req = new SatellitePostRequest
        {
            Date = "2024-01-16",
            Layer = "VIIRS_NOAA21_CorrectedReflectance_TrueColor"
        };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date" && e.ErrorMessage.Contains("2024-01-17"));
    }

    [Fact]
    public void AcceptsDate_GivenValidPastDate()
    {
        var req = new SatellitePostRequest { Date = "2024-01-15" };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void AcceptsDate_GivenExactLayerStartDate()
    {
        var req = new SatellitePostRequest { Date = "2000-02-24" };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void AcceptsDate_GivenNullDate()
    {
        var req = new SatellitePostRequest { Date = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }

    // --- Layer validation (US3) ---

    [Fact]
    public void RejectsLayer_GivenUnsupportedLayer()
    {
        var req = new SatellitePostRequest { Layer = "Unsupported_Layer_Name" };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Layer");
        result.Errors.First(e => e.PropertyName == "Layer").ErrorMessage.ShouldContain("MODIS_Terra");
    }

    [Theory]
    [InlineData("MODIS_Terra_CorrectedReflectance_TrueColor")]
    [InlineData("MODIS_Aqua_CorrectedReflectance_TrueColor")]
    [InlineData("VIIRS_SNPP_CorrectedReflectance_TrueColor")]
    [InlineData("VIIRS_NOAA20_CorrectedReflectance_TrueColor")]
    [InlineData("VIIRS_NOAA21_CorrectedReflectance_TrueColor")]
    public void AcceptsLayer_GivenEachSupportedLayer(string layer)
    {
        var req = new SatellitePostRequest { Layer = layer };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Layer");
    }

    [Fact]
    public void AcceptsLayer_GivenNullLayer()
    {
        var req = new SatellitePostRequest { Layer = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Layer");
    }

    // --- Title validation ---

    [Fact]
    public void IsValid_GivenNullTitle()
    {
        var req = new SatellitePostRequest { Title = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void IsValid_GivenTitleWithin200Characters()
    {
        var req = new SatellitePostRequest { Title = new string('A', 200) };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void RejectsTitle_GivenTitleExceeding200Characters()
    {
        var req = new SatellitePostRequest { Title = new string('A', 201) };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Title");
    }

    // --- Description validation ---

    [Fact]
    public void IsValid_GivenNullDescription()
    {
        var req = new SatellitePostRequest { Description = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void IsValid_GivenDescriptionWithin1000Characters()
    {
        var req = new SatellitePostRequest { Description = new string('A', 1000) };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void RejectsDescription_GivenDescriptionExceeding1000Characters()
    {
        var req = new SatellitePostRequest { Description = new string('A', 1001) };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Description");
    }

    // --- BboxSouth validation ---

    [Fact]
    public void IsValid_GivenNullBboxSouth()
    {
        var req = new SatellitePostRequest { BboxSouth = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "BboxSouth");
    }

    [Theory]
    [InlineData(-90.0)]
    [InlineData(0.0)]
    [InlineData(90.0)]
    public void IsValid_GivenBboxSouthInRange(double value)
    {
        var req = new SatellitePostRequest { BboxSouth = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "BboxSouth");
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void RejectsBboxSouth_GivenValueOutOfRange(double value)
    {
        var req = new SatellitePostRequest { BboxSouth = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "BboxSouth");
    }

    // --- BboxNorth validation ---

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void RejectsBboxNorth_GivenValueOutOfRange(double value)
    {
        var req = new SatellitePostRequest { BboxNorth = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "BboxNorth");
    }

    // --- BboxWest validation ---

    [Theory]
    [InlineData(-180.0)]
    [InlineData(0.0)]
    [InlineData(180.0)]
    public void IsValid_GivenBboxWestInRange(double value)
    {
        var req = new SatellitePostRequest { BboxWest = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "BboxWest");
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void RejectsBboxWest_GivenValueOutOfRange(double value)
    {
        var req = new SatellitePostRequest { BboxWest = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "BboxWest");
    }

    // --- BboxEast validation ---

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void RejectsBboxEast_GivenValueOutOfRange(double value)
    {
        var req = new SatellitePostRequest { BboxEast = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "BboxEast");
    }

    // --- Bbox cross-field validation ---

    [Fact]
    public void RejectsBbox_GivenSouthGreaterThanNorth()
    {
        var req = new SatellitePostRequest { BboxSouth = 42.0, BboxNorth = 38.0 };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("BboxSouth must be less than BboxNorth"));
    }

    [Fact]
    public void RejectsBbox_GivenWestGreaterThanEast()
    {
        var req = new SatellitePostRequest { BboxWest = -80.0, BboxEast = -85.0 };

        var result = _validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("BboxWest must be less than BboxEast"));
    }

    [Fact]
    public void IsValid_GivenValidBboxRange()
    {
        var req = new SatellitePostRequest
        {
            BboxSouth = 38.0,
            BboxWest = -85.0,
            BboxNorth = 42.0,
            BboxEast = -80.0
        };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.ErrorMessage.Contains("Bbox"));
    }

    // --- ImageWidth validation ---

    [Fact]
    public void IsValid_GivenNullImageWidth()
    {
        var req = new SatellitePostRequest { ImageWidth = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "ImageWidth");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(8192)]
    public void IsValid_GivenImageWidthInRange(int value)
    {
        var req = new SatellitePostRequest { ImageWidth = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "ImageWidth");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void RejectsImageWidth_GivenValueOutOfRange(int value)
    {
        var req = new SatellitePostRequest { ImageWidth = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "ImageWidth");
    }

    // --- ImageHeight validation ---

    [Fact]
    public void IsValid_GivenNullImageHeight()
    {
        var req = new SatellitePostRequest { ImageHeight = null };

        var result = _validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "ImageHeight");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void RejectsImageHeight_GivenValueOutOfRange(int value)
    {
        var req = new SatellitePostRequest { ImageHeight = value };

        var result = _validator.Validate(req);

        result.Errors.ShouldContain(e => e.PropertyName == "ImageHeight");
    }
}
