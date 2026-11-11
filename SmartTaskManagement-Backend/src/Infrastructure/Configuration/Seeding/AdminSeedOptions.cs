namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;

/// <summary>
/// First-Admin seed settings, bound from the "AdminSeed" section. The values come only from
/// user-secrets or AdminSeed__* environment variables, never from appsettings. All three
/// empty means seeding is skipped; some but not all is a startup error.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string? FullName { get; init; }

    /// <summary>True when all three settings are present (the validator rejects a partial set).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(FullName);

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Email)
        && string.IsNullOrWhiteSpace(Password)
        && string.IsNullOrWhiteSpace(FullName);
}
