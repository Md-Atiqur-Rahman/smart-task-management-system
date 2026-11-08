using Contoso.TemplateApp.Infrastructure.Configuration.Database;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Contoso.TemplateApp.IntegrationTests.Infrastructure;

/// <summary>Encrypt=false is allowed only in Development and Testing.</summary>
public sealed class DatabaseOptionsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void EncryptFalse_InDevelopmentOrTesting_IsValid(string environment)
    {
        Validate(environment, encrypt: false).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void EncryptFalse_ElsewhereIsRejected(string environment)
    {
        var result = Validate(environment, encrypt: false);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Encrypt");
    }

    [Fact]
    public void EncryptTrue_InProduction_IsValid()
    {
        Validate("Production", encrypt: true).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void ConnectionString_NeverTrustsServerCertificate()
    {
        var connectionString = Options(encrypt: true).BuildConnectionString();

        connectionString.ShouldContain("Encrypt=True");
        connectionString.ShouldNotContain("Trust", Case.Insensitive);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(
        string environment, bool encrypt)
    {
        var hostEnvironment = new HostingEnvironment { EnvironmentName = environment };
        return new DatabaseOptionsValidator(hostEnvironment).Validate(null, Options(encrypt));
    }

    private static DatabaseOptions Options(bool encrypt) => new()
    {
        Host = "localhost",
        Port = 1433,
        Name = "db",
        User = "sa",
        Password = "secret",
        Encrypt = encrypt,
    };
}
