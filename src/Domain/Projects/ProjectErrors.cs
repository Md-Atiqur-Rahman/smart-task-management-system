using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Projects;

/// <summary>Expected failures of the Project aggregate. Codes are part of the API contract.</summary>
public static class ProjectErrors
{
    public static readonly Error NotFound =
        Error.NotFound("projects.not_found", "The project was not found.");

    public static readonly Error InvalidOwner =
        Error.Validation("projects.invalid_owner", "The owner must be an existing Project Manager.");

    public static readonly Error OwnerNotAllowed =
        Error.Validation("projects.owner_not_allowed", "A Project Manager can only create projects they own.");
}
