using Cronos;

namespace BarretApi.Core.Services;

/// <summary>
/// A parsed cron expression bound to a time zone. Wrapping Cronos keeps the
/// time zone and DST rules in one testable place.
/// </summary>
public sealed class CronSchedule
{
    private readonly CronExpression _expression;
    private readonly TimeZoneInfo _timeZone;

    private CronSchedule(CronExpression expression, TimeZoneInfo timeZone)
    {
        _expression = expression;
        _timeZone = timeZone;
    }

    public static bool TryParse(
        string expression,
        string timeZoneId,
        out CronSchedule? schedule,
        out string? error)
    {
        schedule = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Cron expression is required.";
            return false;
        }

        var trimmed = expression.Trim();
        var fieldCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;

        CronExpression parsed;
        try
        {
            parsed = CronExpression.Parse(trimmed, format);
        }
        catch (CronFormatException ex)
        {
            error = $"Cron expression is not valid: {ex.Message}";
            return false;
        }

        TimeZoneInfo timeZone;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZone = TimeZoneInfo.Utc;
        }
        else
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                error = $"Unknown time zone '{timeZoneId}'.";
                return false;
            }
        }

        schedule = new CronSchedule(parsed, timeZone);
        return true;
    }

    /// <summary>
    /// The first occurrence strictly after <paramref name="afterUtc"/>, expressed in UTC.
    /// Returns null when the expression has no further occurrences.
    /// </summary>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc)
    {
        var result = _expression.GetNextOccurrence(afterUtc.ToUniversalTime(), _timeZone, inclusive: false);
        if (result == null)
        {
            return null;
        }

        return result.Value.ToUniversalTime();
    }
}
