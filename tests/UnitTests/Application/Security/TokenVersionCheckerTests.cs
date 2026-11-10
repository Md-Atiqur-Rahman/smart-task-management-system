using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Users;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Security;

public sealed class TokenVersionCheckerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly ITokenVersionStore _store = Substitute.For<ITokenVersionStore>();
    private readonly IUserReadRepository _users = Substitute.For<IUserReadRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    private TokenVersionChecker Checker() => new(_store, _users);

    [Fact]
    public async Task Check_StoreHasSameVersion_Valid()
    {
        _store.GetAsync(_userId, _ct).Returns(3);

        var result = await Checker().CheckAsync(_userId, 3, _ct);

        result.ShouldBe(TokenVersionCheck.Valid);
        await _users.DidNotReceiveWithAnyArgs().GetTokenVersionAsync(default, _ct);
    }

    [Fact]
    public async Task Check_StoreHasHigherVersion_Revoked()
    {
        _store.GetAsync(_userId, _ct).Returns(4);

        var result = await Checker().CheckAsync(_userId, 3, _ct);

        result.ShouldBe(TokenVersionCheck.Revoked);
    }

    [Fact]
    public async Task Check_StoreMiss_LoadsFromDatabaseAndStores()
    {
        _store.GetAsync(_userId, _ct).Returns((int?)null);
        _users.GetTokenVersionAsync(_userId, _ct).Returns(2);

        var result = await Checker().CheckAsync(_userId, 2, _ct);

        result.ShouldBe(TokenVersionCheck.Valid);
        await _store.Received(1).SetIfHigherAsync(_userId, 2, _ct);
    }

    [Fact]
    public async Task Check_StoreMissAndStoreAlreadyHigherAfterLoad_Revoked()
    {
        _store.GetAsync(_userId, _ct).Returns((int?)null, 3);
        _users.GetTokenVersionAsync(_userId, _ct).Returns(2);

        var result = await Checker().CheckAsync(_userId, 2, _ct);

        result.ShouldBe(TokenVersionCheck.Revoked);
    }

    [Fact]
    public async Task Check_StoreMissAndUnknownUser_Revoked()
    {
        _store.GetAsync(_userId, _ct).Returns((int?)null);
        _users.GetTokenVersionAsync(_userId, _ct).Returns((int?)null);

        var result = await Checker().CheckAsync(_userId, 1, _ct);

        result.ShouldBe(TokenVersionCheck.Revoked);
        await _store.DidNotReceiveWithAnyArgs().SetIfHigherAsync(default, default, _ct);
    }

    [Fact]
    public async Task Check_StoreThrowsUnavailable_Unavailable()
    {
        _store.GetAsync(_userId, _ct).ThrowsAsync(new SharedStateUnavailableException());

        var result = await Checker().CheckAsync(_userId, 1, _ct);

        result.ShouldBe(TokenVersionCheck.Unavailable);
    }
}
