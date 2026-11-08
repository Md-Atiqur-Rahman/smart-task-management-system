using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Application.Configuration.Messaging;
using Contoso.TemplateApp.Domain.Results;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Contoso.TemplateApp.UnitTests.Application.Messaging;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public async Task ResolvedHandler_IsDecorated_InvalidCommandNeverReachesHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<TestCommand, Guid>>();

        var result = await handler.Handle(new TestCommand(""), CancellationToken.None);

        result.Error.ShouldBeOfType<ValidationError>();
        scope.ServiceProvider.GetRequiredService<TestCommandHandler>().Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ResolvedHandler_ValidCommand_ReachesHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<TestCommand, Guid>>();

        var result = await handler.Handle(new TestCommand("name"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        scope.ServiceProvider.GetRequiredService<TestCommandHandler>().Calls.ShouldBe(1);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<IValidator<TestCommand>, TestCommandValidator>();
        HandlerRegistration.Register(services, typeof(TestCommandHandler).Assembly);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
