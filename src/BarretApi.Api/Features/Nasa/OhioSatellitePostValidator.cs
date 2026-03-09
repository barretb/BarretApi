using BarretApi.Core.Configuration;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.Nasa;

public sealed class OhioSatellitePostValidator : Validator<OhioSatellitePostRequest>
{
	public OhioSatellitePostValidator(IOptions<NasaGibsOptions> options)
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
			.WithMessage("Each platform must be 'bluesky', 'mastodon', or 'linkedin'.");
	}
}
