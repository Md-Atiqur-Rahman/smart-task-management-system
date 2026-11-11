namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>A new refresh token: the raw value goes to the client, only the hash is stored.</summary>
public sealed record GeneratedRefreshToken(string RawToken, string TokenHash);

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    /// <summary>SHA-256 of the raw token as 64 lower-case hex characters. Never throws for any input.</summary>
    string Hash(string rawToken);
}
