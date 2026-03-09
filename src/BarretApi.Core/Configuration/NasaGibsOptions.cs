namespace BarretApi.Core.Configuration;

/// <summary>
/// Configuration for the NASA GIBS Worldview Snapshot API client.
/// </summary>
public sealed class NasaGibsOptions
{
	public const string SectionName = "NasaGibs";

	public string BaseUrl { get; set; } = "https://wvs.earthdata.nasa.gov/api/v1/snapshot";

	public string DefaultLayer { get; set; } = "MODIS_Terra_CorrectedReflectance_TrueColor";

	public string[] SupportedLayers { get; set; } =
	[
		"MODIS_Terra_CorrectedReflectance_TrueColor",
		"MODIS_Aqua_CorrectedReflectance_TrueColor",
		"VIIRS_SNPP_CorrectedReflectance_TrueColor",
		"VIIRS_NOAA20_CorrectedReflectance_TrueColor",
		"VIIRS_NOAA21_CorrectedReflectance_TrueColor"
	];

	public double BboxSouth { get; set; } = 38.40;

	public double BboxWest { get; set; } = -84.82;

	public double BboxNorth { get; set; } = 42.32;

	public double BboxEast { get; set; } = -80.52;

	public int ImageWidth { get; set; } = 1024;

	public int ImageHeight { get; set; } = 768;

	/// <summary>
	/// Earliest available imagery date per layer. Used for date validation.
	/// </summary>
	public static readonly Dictionary<string, DateOnly> LayerStartDates = new()
	{
		["MODIS_Terra_CorrectedReflectance_TrueColor"] = new DateOnly(2000, 2, 24),
		["MODIS_Aqua_CorrectedReflectance_TrueColor"] = new DateOnly(2002, 7, 4),
		["VIIRS_SNPP_CorrectedReflectance_TrueColor"] = new DateOnly(2015, 11, 24),
		["VIIRS_NOAA20_CorrectedReflectance_TrueColor"] = new DateOnly(2017, 12, 1),
		["VIIRS_NOAA21_CorrectedReflectance_TrueColor"] = new DateOnly(2024, 1, 17)
	};
}
