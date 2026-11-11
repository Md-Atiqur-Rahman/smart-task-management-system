using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ImproveTaskDescription;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Tasks;

public sealed class ImproveTaskDescriptionTests
{
    private readonly ImproveTaskDescriptionCommandValidator _validator = new();
    private readonly IAiTextImprover _improver = Substitute.For<IAiTextImprover>();

    public static TheoryData<Error> ImproverErrors => new()
    {
        AiErrors.NotConfigured,
        AiErrors.Unavailable,
        AiErrors.Rejected,
    };

    private ImproveTaskDescriptionCommandHandler Handler() => new(_improver);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_BlankDescription_Fails(string description)
    {
        _validator.Validate(new ImproveTaskDescriptionCommand(null, description)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_DescriptionOver4000_Fails()
    {
        var description = new string('a', TaskItem.DescriptionMaxLength + 1);

        _validator.Validate(new ImproveTaskDescriptionCommand(null, description)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_TitleOver200_Fails()
    {
        var title = new string('a', TaskItem.TitleMaxLength + 1);

        _validator.Validate(new ImproveTaskDescriptionCommand(title, "d")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_NullTitleAndValidDescription_Passes()
    {
        _validator.Validate(new ImproveTaskDescriptionCommand(null, "d")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_PassesTrimmedTitleAndDescription()
    {
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("x"));

        await Handler().Handle(new ImproveTaskDescriptionCommand("  t  ", "  d  "), CancellationToken.None);
        await Handler().Handle(new ImproveTaskDescriptionCommand("   ", "  d  "), CancellationToken.None);

        await _improver.Received(1).ImproveDescriptionAsync("t", "d", Arg.Any<CancellationToken>());
        await _improver.Received(1).ImproveDescriptionAsync(null, "d", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_ReturnsDto()
    {
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("better"));

        var result = await Handler().Handle(new ImproveTaskDescriptionCommand(null, "d"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe("better");
    }

    [Theory]
    [MemberData(nameof(ImproverErrors))]
    public async Task Handle_ImproverFails_ReturnsSameError(Error error)
    {
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(error));

        var result = await Handler().Handle(new ImproveTaskDescriptionCommand(null, "d"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}
