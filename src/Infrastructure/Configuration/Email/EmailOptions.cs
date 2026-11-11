using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;

/// <summary>
/// SMTP and email outbox settings, bound from the "Email" section (design 8, ADR 0017).
/// <see cref="Password"/> is a secret: user-secrets or the Email__Password environment variable only.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The placeholder that the link templates must contain.</summary>
    public const string IdPlaceholder = "{id}";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    /// <summary>
    /// true = STARTTLS required. false = no TLS, allowed ONLY in Development and Testing (local Mailpit),
    /// the same rule as Database:Encrypt (ADR 0011). Certificate validation is never disabled.
    /// </summary>
    public bool UseTls { get; init; } = true;

    /// <summary>SMTP user; no authentication when empty.</summary>
    public string? UserName { get; init; }

    /// <summary>Secret. Required when <see cref="UserName"/> is set.</summary>
    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Sender display name and subject prefix ("{FromName}: {message}").</summary>
    [Required]
    public string FromName { get; init; } = string.Empty;

    /// <summary>Absolute http(s) base URL of the web app; links are this plus a path template.</summary>
    [Required]
    public Uri? AppBaseUrl { get; init; }

    [Required]
    public string TaskPathTemplate { get; init; } = "/tasks/" + IdPlaceholder;

    [Required]
    public string ProjectPathTemplate { get; init; } = "/projects/" + IdPlaceholder;

    [Range(1, 300)]
    public int PollSeconds { get; init; } = 10;

    [Range(1, 100)]
    public int BatchSize { get; init; } = 20;

    [Range(30, 900)]
    public int ClaimSeconds { get; init; } = 120;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 5;
}
