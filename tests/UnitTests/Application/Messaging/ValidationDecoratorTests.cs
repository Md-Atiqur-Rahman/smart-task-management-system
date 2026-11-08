using Contoso.TemplateApp.Application.Abstractions.Behaviors;
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Domain.Results;

using NSubstitute;

namespace Contoso.TemplateApp.UnitTests.Application.Messaging;

public sealed class ValidationDecoratorTests
{
    private readonly ICommandHandler<TestCommand, Guid> _inner =
        Substitute.For<ICommandHandler<TestCommand, Guid>>();

    [Fact]
    public async Task InvalidCommand_DoesNotCallInnerHandler()
    {
        var decorator = new ValidationDecorator.CommandHandler<TestCommand, Guid>(
            _inner, [new TestCommandValidator()]);

        var result = await decorator.Handle(new TestCommand(""), CancellationToken.None);

        await _inner.DidNotReceive().Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>());
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldContainKey("Name");
    }

    [Fact]
    public async Task ValidCommand_CallsInnerHandler()
    {
        var id = Guid.NewGuid();
        _inner.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(id));
        var decorator = new ValidationDecorator.CommandHandler<TestCommand, Guid>(
            _inner, [new TestCommandValidator()]);

        var result = await decorator.Handle(new TestCommand("name"), CancellationToken.None);

        result.Value.ShouldBe(id);
    }
}
