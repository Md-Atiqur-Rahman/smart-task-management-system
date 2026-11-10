using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Exceptions;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;
using Datavanced.SmartTaskManagement.Infrastructure.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>First-Admin seeding against a real SQL Server (design section 8).</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class AdminSeederTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string SeedPassword = "Seed_Passw0rd!";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // The Admin count is global, so every test starts from a database without Admins.
    public async ValueTask InitializeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.Set<User>().IgnoreQueryFilters().Where(u => u.Role == Role.Admin).ExecuteDeleteAsync(_ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Seed_NoAdminAndSettings_CreatesAdmin()
    {
        var email = UniqueEmail("seed");

        await SeedAsync(email);

        await using var context = fixture.CreateDbContext();
        var admin = await new UserRepository(context).GetByEmailAsync(Email.Create(email), _ct);
        admin.ShouldNotBeNull();
        admin.Role.ShouldBe(Role.Admin);
        admin.PasswordHash.ShouldNotBe(SeedPassword);
    }

    [Fact]
    public async Task Seed_AdminExists_DoesNothing()
    {
        await SeedAsync(UniqueEmail("first"));
        var second = UniqueEmail("second");

        await SeedAsync(second);

        await using var context = fixture.CreateDbContext();
        (await new UserRepository(context).CountByRoleAsync(Role.Admin, _ct)).ShouldBe(1);
        (await new UserRepository(context).EmailExistsAsync(Email.Create(second), _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Seed_TwoSeedersInParallel_ExactlyOneAdmin()
    {
        var email = UniqueEmail("race");

        await Task.WhenAll(SeedAsync(email), SeedAsync(email));

        await using var context = fixture.CreateDbContext();
        (await new UserRepository(context).CountByRoleAsync(Role.Admin, _ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Seed_EmailBelongsToTeamMember_ThrowsAndDoesNotPromote()
    {
        var email = UniqueEmail("member");
        await using (var context = fixture.CreateDbContext())
        {
            new UserRepository(context).Add(User.Register(Email.Create(email), "Member", "hash"));
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        var exception = await Should.ThrowAsync<AppException>(() => SeedAsync(email));

        exception.Message.ShouldContain("AdminSeed:Email");
        await using var check = fixture.CreateDbContext();
        var user = await new UserRepository(check).GetByEmailAsync(Email.Create(email), _ct);
        user.ShouldNotBeNull();
        user.Role.ShouldBe(Role.TeamMember);
        (await new UserRepository(check).CountByRoleAsync(Role.Admin, _ct)).ShouldBe(0);
    }

    [Fact]
    public void Validator_AllEmpty_IsValid()
    {
        new AdminSeedOptionsValidator().Validate(null, new AdminSeedOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validator_OnlyEmail_Fails()
    {
        var result = new AdminSeedOptionsValidator().Validate(null, new AdminSeedOptions { Email = "a@example.com" });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AdminSeed");
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Validator_PasswordBreaksPolicy_Fails(string password)
    {
        var options = new AdminSeedOptions { Email = "a@example.com", Password = password, FullName = "Admin" };

        new AdminSeedOptionsValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validator_AllValid_Succeeds()
    {
        var options = new AdminSeedOptions { Email = "a@example.com", Password = SeedPassword, FullName = "Admin" };

        new AdminSeedOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    private static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@example.com";

    // Each call gets its own context (its own scope), like the hosted service.
    private async Task SeedAsync(string email)
    {
        await using var context = fixture.CreateDbContext();
        var seeder = new AdminSeeder(
            new UserRepository(context),
            new UnitOfWork(context),
            new TestHasher(),
            Options.Create(new AdminSeedOptions { Email = email, Password = SeedPassword, FullName = "Seed Admin" }),
            NullLogger<AdminSeeder>.Instance);
        await seeder.SeedAsync(_ct);
    }

    private sealed class TestHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password.Length;

        public bool Verify(string passwordHash, string providedPassword) => passwordHash == Hash(providedPassword);
    }
}
