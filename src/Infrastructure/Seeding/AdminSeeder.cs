using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Seeding;

/// <summary>
/// Creates the first Admin from AdminSeed settings (design section 8). Skips when an Admin exists or the
/// settings are empty. An existing non-Admin account with the seed email is NEVER promoted: startup fails.
/// </summary>
public sealed partial class AdminSeeder(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IOptions<AdminSeedOptions> options,
    ILogger<AdminSeeder> logger)
{
    public const string SeedEmailConflictCode = "seeding.admin_email_in_use";

    public const string SeedFailedCode = "seeding.admin_failed";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            LogSkippedNoSettings();
            return;
        }

        if (await users.CountByRoleAsync(Role.Admin, cancellationToken) > 0)
        {
            LogSkippedAdminExists();
            return;
        }

        var admin = User.CreateSeedAdmin(
            Domain.Users.Email.Create(settings.Email!),
            settings.FullName!.Trim(),
            passwordHasher.Hash(settings.Password!));
        users.Add(admin);

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            LogSeeded(admin.Id);
            return;
        }

        if (result.Error.Code != PersistenceErrors.DuplicateKeyCode)
        {
            throw new AdminSeedException(SeedFailedCode, $"Seeding the first Admin failed: {result.Error.Code}.");
        }

        // Unique email violation: a parallel instance may have won, or the email belongs to another account.
        if (await users.CountByRoleAsync(Role.Admin, cancellationToken) > 0)
        {
            LogParallelInstanceWon();
            return;
        }

        throw new AdminSeedException(
            SeedEmailConflictCode,
            "AdminSeed:Email belongs to an existing non-Admin account, which is never promoted. "
            + "Set a different AdminSeed:Email and restart.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin seeding skipped: AdminSeed settings are not set.")]
    private partial void LogSkippedNoSettings();

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin seeding skipped: an Admin already exists.")]
    private partial void LogSkippedAdminExists();

    [LoggerMessage(Level = LogLevel.Information, Message = "First Admin seeded (user {UserId}).")]
    private partial void LogSeeded(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin seeding: another instance created the Admin first; continuing.")]
    private partial void LogParallelInstanceWon();
}
