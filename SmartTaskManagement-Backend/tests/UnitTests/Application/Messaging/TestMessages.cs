using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Results;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Messaging;

internal sealed record TestCommand(string Name) : ICommand<Guid>;

internal sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}

/// <summary>Concrete handler used to test the DI registration and decoration.</summary>
internal sealed class TestCommandHandler : ICommandHandler<TestCommand, Guid>
{
    public int Calls { get; private set; }

    public Task<Result<Guid>> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result.Success(Guid.NewGuid()));
    }
}
