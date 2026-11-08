using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Testcontainers.MsSql;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// One real SQL Server container per test collection. The image comes from
/// .env.example (same pin as docker-compose.yml). The connection string is built
/// by our own DatabaseOptions (Encrypt=false, Testing only); the container's own
/// connection string is NOT used.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Password = "Integration_Tests_Passw0rd!";

    private readonly MsSqlContainer _container =
        new MsSqlBuilder(RepositoryFiles.EnvExample("SQLSERVER_IMAGE"))
            .WithPassword(Password)
            .Build();

    public FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));

    public DatabaseOptions Options => new()
    {
        Host = _container.Hostname,
        Port = _container.GetMappedPublicPort(MsSqlBuilder.MsSqlPort),
        Name = "IntegrationTests",
        User = "sa",
        Password = Password,
        Encrypt = false,
    };

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(Options.BuildConnectionString())
            .AddInterceptors(new AuditInterceptor(Time))
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollectionDefinition : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
