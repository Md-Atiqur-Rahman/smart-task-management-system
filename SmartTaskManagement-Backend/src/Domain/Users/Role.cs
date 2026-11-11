namespace Datavanced.SmartTaskManagement.Domain.Users;

/// <summary>Authorization role of a user. Values are persisted; do not renumber.</summary>
public enum Role
{
    Admin = 0,
    ProjectManager = 1,
    TeamMember = 2,
}
