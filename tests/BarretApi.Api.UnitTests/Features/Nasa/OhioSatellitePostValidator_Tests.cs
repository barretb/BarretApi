using BarretApi.Api.Features.Nasa;
using BarretApi.Core.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Nasa;

public sealed class OhioSatellitePostValidator_Tests
{
	private readonly OhioSatellitePostValidator _validator;

	public OhioSatellitePostValidator_Tests()
	{
		var options = Substitute.For<IOptions<NasaGibsOptions>>();
		options.Value.Returns(new NasaGibsOptions());
		_validator = new OhioSatellitePostValidator(options);
	}

	// --- Platforms validation ---

	[Fact]
	public void IsValid_GivenEmptyRequest()
	{
		var req = new OhioSatellitePostRequest();

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_GivenNullPlatforms()
	{
		var req = new OhioSatellitePostRequest { Platforms = null };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_GivenValidPlatforms()
	{
		var req = new OhioSatellitePostRequest { Platforms = ["bluesky", "mastodon", "linkedin"] };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_GivenCaseInsensitivePlatforms()
	{
		var req = new OhioSatellitePostRequest { Platforms = ["Bluesky", "MASTODON", "LinkedIn"] };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void RejectsPlatform_GivenInvalidPlatformName()
	{
		var req = new OhioSatellitePostRequest { Platforms = ["twitter"] };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
	}

	[Fact]
	public void RejectsPlatforms_GivenMixedValidAndInvalidPlatforms()
	{
		var req = new OhioSatellitePostRequest { Platforms = ["bluesky", "twitter"] };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
	}

	[Fact]
	public void IsValid_GivenEmptyPlatformsList()
	{
		var req = new OhioSatellitePostRequest { Platforms = [] };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeTrue();
	}

	// --- Date validation (US2) ---

	[Fact]
	public void RejectsDate_GivenInvalidDateFormat()
	{
		var req = new OhioSatellitePostRequest { Date = "not-a-date" };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Date");
	}

	[Fact]
	public void RejectsDate_GivenFutureDate()
	{
		var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
		var req = new OhioSatellitePostRequest { Date = tomorrow.ToString("yyyy-MM-dd") };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Date");
	}

	[Fact]
	public void RejectsDate_GivenDateBeforeModisTerraStart()
	{
		var req = new OhioSatellitePostRequest { Date = "2000-02-23" };

		var result = _validator.Validate(req);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Date" && e.ErrorMessage.Contains("2000-02-24"));
	}

	[Fact]
	public void RejectsDate_GivenDateBeforeViirsSNOAA21Start()
	{
		var req = new OhioSatellitePostRequest
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
		var req = new OhioSatellitePostRequest { Date = "2024-01-15" };

		var result = _validator.Validate(req);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
	}

	[Fact]
	public void AcceptsDate_GivenExactLayerStartDate()
	{
		var req = new OhioSatellitePostRequest { Date = "2000-02-24" };

		var result = _validator.Validate(req);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
	}

	[Fact]
	public void AcceptsDate_GivenNullDate()
	{
		var req = new OhioSatellitePostRequest { Date = null };

		var result = _validator.Validate(req);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
	}

	// --- Layer validation (US3) ---

	[Fact]
	public void RejectsLayer_GivenUnsupportedLayer()
	{
		var req = new OhioSatellitePostRequest { Layer = "Unsupported_Layer_Name" };

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
		var req = new OhioSatellitePostRequest { Layer = layer };

		var result = _validator.Validate(req);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Layer");
	}

	[Fact]
	public void AcceptsLayer_GivenNullLayer()
	{
		var req = new OhioSatellitePostRequest { Layer = null };

		var result = _validator.Validate(req);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Layer");
	}
}
