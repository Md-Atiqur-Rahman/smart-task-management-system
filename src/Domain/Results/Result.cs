namespace Contoso.TemplateApp.Domain.Results;

/// <summary>Outcome of an operation that can fail in an expected way.</summary>
public class Result
{
    private readonly Error? _error;

    protected Result(Error? error)
    {
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure. Reading it on a successful result is a programming error.</summary>
    public Error Error => _error
        ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, null);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, error);

    /// <summary>Success with the value, or failure with <paramref name="error"/> when it is null.</summary>
    public static Result<TValue> FromNullable<TValue>(TValue? value, Error error) =>
        value is null ? Failure<TValue>(error) : Success(value);
}
