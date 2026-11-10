using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>User and refresh token persistence against a real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class UserPersistenceTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddUser_ThenGetByEmail_ReturnsUserWithNormalizedEmail()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"mixed.case.{suffix}@example.com";
        var user = User.Register(Email.Create(email), "Mixed Case", "hash");
        await SaveUserAsync(user);

        await using var context = fixture.CreateDbContext();
        var loaded = await new UserRepository(context).GetByEmailAsync(Email.Create($"Mixed.CASE.{suffix}@Example.com"), _ct);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(user.Id);
        loaded.Email.Value.ShouldBe(email);
        loaded.Role.ShouldBe(Role.TeamMember);
        loaded.TokenVersion.ShouldBe(1);
        (await new UserRepository(context).EmailExistsAsync(Email.Create(email), _ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChanges_DuplicateEmail_ReturnsDuplicateKeyConflict()
    {
        var email = UniqueEmail("dupkey");
        await SaveUserAsync(User.Register(Email.Create(email), "First", "hash"));

        await using var context = fixture.CreateDbContext();
        new UserRepository(context).Add(User.Register(Email.Create(email), "Second", "hash"));

        var result = await new UnitOfWork(context).SaveChangesAsync(_ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("persistence.duplicate_key");
    }

    [Fact]
    public async Task CountByRole_TwoAdminsOneMember_ReturnsTwo()
    {
        int adminsBefore;
        int membersBefore;
        await using (var before = fixture.CreateDbContext())
        {
            var beforeRepository = new UserRepository(before);
            adminsBefore = await beforeRepository.CountByRoleAsync(Role.Admin, _ct);
            membersBefore = await beforeRepository.CountByRoleAsync(Role.TeamMember, _ct);
        }

        await SaveUserAsync(User.CreateSeedAdmin(Email.Create(UniqueEmail("a1")), "Admin One", "hash"));
        await SaveUserAsync(User.CreateSeedAdmin(Email.Create(UniqueEmail("a2")), "Admin Two", "hash"));
        await SaveUserAsync(User.Register(Email.Create(UniqueEmail("m1")), "Member", "hash"));

        await using var context = fixture.CreateDbContext();
        var repository = new UserRepository(context);

        (await repository.CountByRoleAsync(Role.Admin, _ct)).ShouldBe(adminsBefore + 2);
        (await repository.CountByRoleAsync(Role.TeamMember, _ct)).ShouldBe(membersBefore + 1);
    }

    [Fact]
    public async Task RevokeActiveInFamily_RevokesOnlyActiveTokensOfThatFamily()
    {
        var now = fixture.Time.GetUtcNow();
        var userId = Guid.NewGuid();
        var root = RefreshToken.Issue(userId, UniqueHash(), now, TimeSpan.FromDays(7));
        var active = RefreshToken.IssueInFamily(userId, UniqueHash(), now, TimeSpan.FromDays(7), root.FamilyId);
        var revoked = RefreshToken.IssueInFamily(userId, UniqueHash(), now, TimeSpan.FromDays(7), root.FamilyId);
        var revokedAt = now.AddMinutes(-5);
        revoked.Revoke(revokedAt, null).IsSuccess.ShouldBeTrue();
        var expired = RefreshToken.IssueInFamily(userId, UniqueHash(), now.AddDays(-10), TimeSpan.FromDays(7), root.FamilyId);
        var otherFamilySameUser = RefreshToken.Issue(userId, UniqueHash(), now, TimeSpan.FromDays(7));

        await using (var context = fixture.CreateDbContext())
        {
            var repository = new RefreshTokenRepository(context);
            foreach (var token in new[] { root, active, revoked, expired, otherFamilySameUser })
            {
                repository.Add(token);
            }

            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var actionContext = fixture.CreateDbContext();
        var count = await new RefreshTokenRepository(actionContext).RevokeActiveInFamilyAsync(root.FamilyId, now, _ct);

        count.ShouldBe(2);
        await using var verify = fixture.CreateDbContext();
        var repo = new RefreshTokenRepository(verify);
        async Task<RefreshToken> Load(RefreshToken t) => (await repo.GetByHashAsync(t.TokenHash, _ct)).ShouldNotBeNull();
        (await Load(root)).RevokedAt.ShouldNotBeNull();
        (await Load(active)).RevokedAt.ShouldNotBeNull();
        (await Load(active)).FamilyId.ShouldBe(root.Id);
        (await Load(revoked)).RevokedAt.ShouldBe(revokedAt);
        (await Load(expired)).RevokedAt.ShouldBeNull();
        (await Load(otherFamilySameUser)).RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Migration_AddRefreshTokenFamilies_BackfillsFamilyIdWithOwnId()
    {
        const string BeforeFamilies = "20261109174148_AddUsersAndRefreshTokens";
        var options = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var id = Guid.NewGuid();
        var now = fixture.Time.GetUtcNow();

        await using (var context = fixture.CreateDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync(BeforeFamilies, cancellationToken: _ct);
            var hash = UniqueHash();
            var expiresAt = now.AddDays(7);
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO [RefreshTokens] ([Id], [UserId], [TokenHash], [CreatedAt], [ExpiresAt]) VALUES ({id}, {Guid.NewGuid()}, {hash}, {now}, {expiresAt})",
                _ct);
        }

        await using (var context = fixture.CreateDbContext(options))
        {
            await context.Database.MigrateAsync(_ct);
            var token = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(t => t.Id == id, _ct);

            token.FamilyId.ShouldBe(id);
        }
    }

    [Fact]
    public async Task ListActiveForUser_IgnoresRevokedExpiredAndOtherUsers()
    {
        var now = fixture.Time.GetUtcNow();
        var userId = Guid.NewGuid();
        var hashes = new[] { UniqueHash(), UniqueHash(), UniqueHash(), UniqueHash() };
        var active = RefreshToken.Issue(userId, hashes[0], now, TimeSpan.FromDays(7));
        var revoked = RefreshToken.Issue(userId, hashes[1], now, TimeSpan.FromDays(7));
        revoked.Revoke(now, null).IsSuccess.ShouldBeTrue();
        var expired = RefreshToken.Issue(userId, hashes[2], now.AddDays(-10), TimeSpan.FromDays(7));
        var other = RefreshToken.Issue(Guid.NewGuid(), hashes[3], now, TimeSpan.FromDays(7));

        await using (var context = fixture.CreateDbContext())
        {
            var repository = new RefreshTokenRepository(context);
            repository.Add(active);
            repository.Add(revoked);
            repository.Add(expired);
            repository.Add(other);
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        var repo = new RefreshTokenRepository(verify);
        var list = await repo.ListActiveForUserAsync(userId, now, _ct);

        list.Count.ShouldBe(1);
        list[0].Id.ShouldBe(active.Id);
        (await repo.GetByHashAsync(hashes[1], _ct)).ShouldNotBeNull().Id.ShouldBe(revoked.Id);
    }

    [Fact]
    public async Task RevokeSameToken_FromTwoContexts_SecondSaveIsConcurrencyConflict()
    {
        var now = fixture.Time.GetUtcNow();
        var tokenHash = UniqueHash();
        var token = RefreshToken.Issue(Guid.NewGuid(), tokenHash, now, TimeSpan.FromDays(7));
        await using (var context = fixture.CreateDbContext())
        {
            new RefreshTokenRepository(context).Add(token);
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstCopy = await new RefreshTokenRepository(first).GetByHashAsync(tokenHash, _ct);
        var secondCopy = await new RefreshTokenRepository(second).GetByHashAsync(tokenHash, _ct);

        firstCopy.ShouldNotBeNull().Revoke(now, null).IsSuccess.ShouldBeTrue();
        secondCopy.ShouldNotBeNull().Revoke(now, null).IsSuccess.ShouldBeTrue();

        (await new UnitOfWork(first).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        var result = await new UnitOfWork(second).SaveChangesAsync(_ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("concurrency.conflict");
    }

    private static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@example.com";

    private static string UniqueHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private async Task SaveUserAsync(User user)
    {
        await using var context = fixture.CreateDbContext();
        new UserRepository(context).Add(user);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
