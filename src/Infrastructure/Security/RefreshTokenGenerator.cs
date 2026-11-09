using System.Security.Cryptography;
using System.Text;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.Infrastructure.Security;

/// <summary>32 random bytes as base64url; stored as a SHA-256 hex hash.</summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        return new GeneratedRefreshToken(raw, Hash(raw));
    }

    public string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
