using System.ComponentModel.DataAnnotations;

using Microsoft.Data.SqlClient;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;

/// <summary>
/// SQL Server connection settings, bound from the "Database" section. No secret
/// and no database name live in appsettings; they come from user-secrets or
/// environment variables (Database__Name, Database__Password).
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 1433;

    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string User { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// true = TLS required (Mandatory). false = no TLS, allowed ONLY in Development
    /// and Testing (local and Testcontainers SQL Server). Certificate validation is
    /// never disabled. See ADR 0011.
    /// </summary>
    public bool Encrypt { get; init; } = true;

    public string BuildConnectionString() =>
        new SqlConnectionStringBuilder
        {
            DataSource = $"{Host},{Port}",
            InitialCatalog = Name,
            UserID = User,
            Password = Password,
            Encrypt = Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
        }.ConnectionString;
}
