using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Users;

/// <summary>Expected failures of the User aggregate. Codes are part of the API contract.</summary>
public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("users.not_found", "The user was not found.");

    public static readonly Error EmailTaken =
        Error.Conflict("users.email_taken", "The email address is already registered.");

    public static readonly Error LastAdmin =
        Error.Conflict("users.last_admin", "The last administrator cannot be changed or removed.");
}
