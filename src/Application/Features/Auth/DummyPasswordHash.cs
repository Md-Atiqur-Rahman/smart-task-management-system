using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>
/// Hash of a random throwaway password, created once on first use. Login verifies against it
/// when the email is unknown, so an unknown email costs one hash verification like a wrong
/// password and response time does not reveal which emails exist. Singleton.
/// </summary>
internal sealed class DummyPasswordHash(IPasswordHasher passwordHasher)
{
    private readonly Lazy<string> _value = new(
        () => passwordHasher.Hash(Guid.NewGuid().ToString("N")),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Value => _value.Value;
}
