namespace BarretApi.Core.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; } = false;
    public required string SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public required string ToAddress { get; init; }
}
