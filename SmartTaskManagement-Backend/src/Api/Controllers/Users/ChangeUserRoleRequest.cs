using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Users;

/// <summary>
/// Body of PUT /api/users/{id}/role. Nullable and [Required] so a missing role is a 400
/// instead of silently becoming the enum default (Admin).
/// </summary>
public sealed record ChangeUserRoleRequest([Required] Role? Role);
