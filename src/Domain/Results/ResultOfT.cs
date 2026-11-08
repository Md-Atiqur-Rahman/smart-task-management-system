namespace Contoso.TemplateApp.Domain.Results;

/// <summary>Outcome of an operation that returns a value or fails in an expected way.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, Error? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>The value. Reading it on a failed result is a programming error.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");
}
