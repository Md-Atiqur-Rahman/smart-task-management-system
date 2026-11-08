using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain;

public sealed class ResultTests
{
    private static readonly Error NotFound =
        Error.NotFound("sample.not_found", "Sample was not found.");

    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_ExposesError()
    {
        var result = Result.Failure(NotFound);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_Value_Throws()
    {
        var result = Result.Failure<int>(NotFound);

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void FromNullable_NullValue_ReturnsGivenError()
    {
        // Also proves: an unconstrained generic T? parameter accepts null
        // under the woven assembly (NullGuard Mode=Implicit).
        string? missing = null;

        var result = Result.FromNullable(missing, NotFound);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(NotFound);
    }

    [Fact]
    public void FromNullable_Value_ReturnsSuccess()
    {
        var result = Result.FromNullable("found", NotFound);

        result.Value.ShouldBe("found");
    }

    [Fact]
    public void ValidationError_CarriesFieldErrors()
    {
        var error = new ValidationError(
            new Dictionary<string, string[]> { ["Name"] = ["Name is required."] });

        error.Type.ShouldBe(ErrorType.Validation);
        error.Errors["Name"].ShouldBe(["Name is required."]);
    }
}
