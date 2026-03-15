using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.SocialPost;

public sealed class TriggerRssPromotionValidator : Validator<TriggerRssPromotionRequest>
{
    public TriggerRssPromotionValidator()
    {
        RuleFor(x => x.FeedUrl)
            .Must(BeAValidAbsoluteHttpUrl!)
            .When(x => !string.IsNullOrWhiteSpace(x.FeedUrl))
            .WithMessage("Feed URL must be a valid absolute URL with http or https scheme.");

        RuleFor(x => x.RecentDaysWindow)
            .GreaterThan(0)
            .When(x => x.RecentDaysWindow.HasValue)
            .WithMessage("RecentDaysWindow must be greater than 0 when provided.");
    }

    private static bool BeAValidAbsoluteHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }
}
