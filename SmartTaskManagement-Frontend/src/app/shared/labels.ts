import { Role, TaskItemStatus, TaskPriority } from '../core/models/api.models';

/** Display labels for the API's enum strings (one place). */
const STATUS_LABELS: Readonly<Record<TaskItemStatus, string>> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

const PRIORITY_LABELS: Readonly<Record<TaskPriority, string>> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
};

const ROLE_LABELS: Readonly<Record<Role, string>> = {
  Admin: 'Admin',
  ProjectManager: 'Project Manager',
  TeamMember: 'Team Member',
};

export function statusLabel(status: TaskItemStatus): string {
  return STATUS_LABELS[status];
}

export function priorityLabel(priority: TaskPriority): string {
  return PRIORITY_LABELS[priority];
}

export function roleLabel(role: Role): string {
  return ROLE_LABELS[role];
}
