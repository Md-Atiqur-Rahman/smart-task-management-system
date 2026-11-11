namespace Datavanced.SmartTaskManagement.Domain.Results;

/// <summary>
/// Kind of expected failure. The Api maps it to an HTTP status in one place.
/// Never persisted: it lives only in Result/Error, in memory.
/// </summary>
public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Forbidden = 3,
    Unauthorized = 4,
    Unavailable = 5,
}
