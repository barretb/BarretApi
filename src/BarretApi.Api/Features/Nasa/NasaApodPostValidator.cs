using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Nasa;

public sealed class NasaApodPostValidator : Validator<NasaApodPostRequest>
{
	private static readonly DateOnly FirstApodDate = new(1995, 6, 16);

	public NasaApodPostValidator()
	{
		RuleFor(x => x.Date)
			.Must(date => DateOnly.TryParseExact(date, "yyyy-MM-dd", out _))
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage("Date must be in YYYY-MM-DD format.");

		RuleFor(x => x.Date)
			.Must(date =>
			{
				if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
				{
					return true; // Format rule handles this
				}

				return parsed <= DateOnly.FromDateTime(DateTime.UtcNow);
			})
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage("Date must not be in the future.");

		RuleFor(x => x.Date)
			.Must(date =>
			{
				if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
				{
					return true; // Format rule handles this
				}

				return parsed >= FirstApodDate;
			})
			.When(x => !string.IsNullOrWhiteSpace(x.Date))
			.WithMessage($"Date must not be before {FirstApodDate:yyyy-MM-dd}, the first APOD.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be 'bluesky', 'mastodon', or 'linkedin'.");
	}
}
