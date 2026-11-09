namespace Datavanced.SmartTaskManagement.Domain.Exceptions;

/// <summary>
/// Base for unexpected or invariant failures. Concrete exceptions are sealed.
/// The Api turns any exception into a generic 500; details go only to the log.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string errorCode, string title, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        Title = title;
    }

    protected AppException(string errorCode, string title, string message, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Title = title;
    }

    public string ErrorCode { get; }

    public string Title { get; }
}
