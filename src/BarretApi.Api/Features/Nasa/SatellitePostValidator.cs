using BarretApi.Core.Configuration;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.Nasa;

public sealed class SatellitePostValidator : Validator<SatellitePostRequest>
{
	public SatellitePostValidator(IOptions<NasaGibsOptions> options)
	{
		var gibsOptions = options.Value;

		RuleFor(x => x.Date)
			.Must(date => DateOnly.TryParseExact(date, "yyyy-MM-dd", out _))
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage("Date must be in YYYY-MM-DD format.");

		RuleFor(x => x.Date)
			.Must(date =>
			{
				if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
				{
					return true;
				}

				return parsed <= DateOnly.FromDateTime(DateTime.UtcNow);
			})
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage("Date must not be in the future.");

		RuleFor(x => x.Date)
			.Must((req, date) =>
			{
				if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
				{
					return true;
				}

				var layer = req.Layer ?? gibsOptions.DefaultLayer;
				if (NasaGibsOptions.LayerStartDates.TryGetValue(layer, out var startDate))
				{
					return parsed >= startDate;
				}

				return true;
			})
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage(req =>
			{
				var layer = req.Layer ?? gibsOptions.DefaultLayer;
				if (NasaGibsOptions.LayerStartDates.TryGetValue(layer, out var startDate))
				{
					return $"Date must not be before {startDate:yyyy-MM-dd}, the earliest date for {layer}.";
				}

				return "Date is before the layer's earliest available date.";
			});

		RuleFor(x => x.Layer)
			.Must(layer => gibsOptions.SupportedLayers.Contains(layer!, StringComparer.Ordinal))
			.When(x => !string.IsNullOrWhiteSpace(x.Layer))
			.WithMessage($"Layer must be one of: {string.Join(", ", gibsOptions.SupportedLayers)}.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be one of: bluesky, mastodon, linkedin.");

		RuleFor(x => x.Title)
			.MaximumLength(200)
			.When(x => !string.IsNullOrWhiteSpace(x.Title))
			.WithMessage("Title must not exceed 200 characters.");

		RuleFor(x => x.Description)
			.MaximumLength(1000)
			.When(x => !string.IsNullOrWhiteSpace(x.Description))
			.WithMessage("Description must not exceed 1000 characters.");

		RuleFor(x => x.BboxSouth)
			.InclusiveBetween(-90.0, 90.0)
			.When(x => x.BboxSouth is not null)
			.WithMessage("BboxSouth must be between -90 and 90.");

		RuleFor(x => x.BboxNorth)
			.InclusiveBetween(-90.0, 90.0)
			.When(x => x.BboxNorth is not null)
			.WithMessage("BboxNorth must be between -90 and 90.");

		RuleFor(x => x.BboxWest)
			.InclusiveBetween(-180.0, 180.0)
			.When(x => x.BboxWest is not null)
			.WithMessage("BboxWest must be between -180 and 180.");

		RuleFor(x => x.BboxEast)
			.InclusiveBetween(-180.0, 180.0)
			.When(x => x.BboxEast is not null)
			.WithMessage("BboxEast must be between -180 and 180.");

		RuleFor(x => x)
			.Must(x => x.BboxSouth!.Value < x.BboxNorth!.Value)
			.When(x => x.BboxSouth is not null && x.BboxNorth is not null)
			.WithMessage("BboxSouth must be less than BboxNorth.");

		RuleFor(x => x)
			.Must(x => x.BboxWest!.Value < x.BboxEast!.Value)
			.When(x => x.BboxWest is not null && x.BboxEast is not null)
			.WithMessage("BboxWest must be less than BboxEast.");

		RuleFor(x => x.ImageWidth)
			.InclusiveBetween(1, 8192)
			.When(x => x.ImageWidth is not null)
			.WithMessage("ImageWidth must be between 1 and 8192.");

		RuleFor(x => x.ImageHeight)
			.InclusiveBetween(1, 8192)
			.When(x => x.ImageHeight is not null)
			.WithMessage("ImageHeight must be between 1 and 8192.");
	}
}
