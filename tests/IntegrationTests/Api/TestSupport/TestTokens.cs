using System.Text;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>Creates real HMAC-SHA256 signed JWTs for the test host's Jwt settings.</summary>
public static class TestTokens
{
    public const string Issuer = "test-issuer";
    public const string Audience = "test-audience";
    public const string SigningKey = "integration-test-signing-key-0123456789-abcdef";

    public static string Create(IDictionary<string, object> claims, string signingKey = SigningKey)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
