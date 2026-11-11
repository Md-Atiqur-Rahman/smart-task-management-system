import { Role } from '../../core/models/api.models';

/**
 * UX hints only (the API decides). The task DTOs carry no project owner, so the
 * owner is passed when the page knows it (project and task pages) and is null
 * in the all-tasks list (frontend-design.md decision 1).
 */
export function canManageTask(role: Role, me: string, projectOwnerId: string | null): boolean {
  return role === 'Admin' || (role === 'ProjectManager' && projectOwnerId === me);
}

/** The assignee (any role), an Admin, or the owning Project Manager may change the status. */
export function canChangeStatus(
  role: Role,
  me: string,
  assigneeId: string | null,
  projectOwnerId: string | null,
): boolean {
  return assigneeId === me || canManageTask(role, me, projectOwnerId);
}
