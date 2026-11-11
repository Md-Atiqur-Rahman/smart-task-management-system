// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CreateSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Samples;

public sealed class SampleCommandHandlerTests
{
    private readonly ISampleRepository _repository = Substitute.For<ISampleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public SampleCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    [Fact]
    public async Task Create_AddsSampleAndSaves_ReturnsId()
    {
        var handler = new CreateSampleCommandHandler(_repository, _unitOfWork);

        var result = await handler.Handle(new CreateSampleCommand("name", null), CancellationToken.None);

        result.Value.ShouldNotBe(Guid.Empty);
        _repository.Received(1).Add(Arg.Is<Sample>(s => s.Id == result.Value && s.Name == "name"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_Missing_ReturnsNotFound()
    {
        var handler = new CompleteSampleCommandHandler(_repository, _unitOfWork);

        var result = await handler.Handle(new CompleteSampleCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(SampleErrors.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_ReturnsConflict_WithoutSaving()
    {
        var sample = Sample.Create("name", null);
        sample.Complete();
        _repository.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var handler = new CompleteSampleCommandHandler(_repository, _unitOfWork);

        var result = await handler.Handle(new CompleteSampleCommand(sample.Id), CancellationToken.None);

        result.Error.ShouldBe(SampleErrors.AlreadyCompleted);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_SaveConflict_IsReturned()
    {
        var sample = Sample.Create("name", null);
        _repository.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var conflict = Error.Conflict("concurrency.conflict", "changed");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(conflict));
        var handler = new CompleteSampleCommandHandler(_repository, _unitOfWork);

        var result = await handler.Handle(new CompleteSampleCommand(sample.Id), CancellationToken.None);

        result.Error.ShouldBe(conflict);
    }

    [Fact]
    public async Task Delete_MarksDeletedWithTimeProviderTime()
    {
        var sample = Sample.Create("name", null);
        _repository.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var handler = new DeleteSampleCommandHandler(_repository, _unitOfWork, _time);

        var result = await handler.Handle(new DeleteSampleCommand(sample.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        sample.IsDeleted.ShouldBeTrue();
        sample.DeletedAt.ShouldBe(_time.GetUtcNow());
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        var handler = new DeleteSampleCommandHandler(_repository, _unitOfWork, _time);

        var result = await handler.Handle(new DeleteSampleCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(SampleErrors.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateValidator_BlankName_Fails(string name)
    {
        var result = new CreateSampleCommandValidator().Validate(new CreateSampleCommand(name, null));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateSampleCommand.Name));
    }

    [Fact]
    public void CreateValidator_TooLongName_Fails()
    {
        var result = new CreateSampleCommandValidator()
            .Validate(new CreateSampleCommand(new string('x', Sample.NameMaxLength + 1), null));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_TooLongDescription_Fails()
    {
        var result = new CreateSampleCommandValidator()
            .Validate(new CreateSampleCommand("name", new string('x', Sample.DescriptionMaxLength + 1)));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateSampleCommand.Description));
    }
}
