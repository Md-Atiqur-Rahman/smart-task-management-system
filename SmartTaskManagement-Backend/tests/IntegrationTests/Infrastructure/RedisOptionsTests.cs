using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Redis without ssl=true is allowed only in Development and Testing.</summary>
public sealed class RedisOptionsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void NoSsl_InDevelopmentOrTesting_IsValid(string environment)
    {
        Validate(environment, "localhost:6379,password=x").Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NoSsl_ElsewhereIsRejected(string environment)
    {
        var result = Validate(environment, "localhost:6379,password=x,ssl=false");

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ssl");
    }

    [Fact]
    public void SslTrue_InProduction_IsValid()
    {
        Validate("Production", "localhost:6380,password=x,ssl=true").Succeeded.ShouldBeTrue();
    }

    private static ValidateOptionsResult Validate(string environment, string connectionString)
    {
        var hostEnvironment = new HostingEnvironment { EnvironmentName = environment };
        return new RedisOptionsValidator(hostEnvironment)
            .Validate(null, new RedisOptions { ConnectionString = connectionString });
    }
}
