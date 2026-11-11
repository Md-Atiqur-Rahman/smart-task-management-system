/**
 * Shared API types, named and shaped like the schemas in
 * SmartTaskManagement-Backend/docs/openapi.json. Enums are the API's strings.
 */

export type Role = 'Admin' | 'ProjectManager' | 'TeamMember';
export type TaskItemStatus = 'ToDo' | 'InProgress' | 'Completed' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type SortDirection = 'Ascending' | 'Descending';

export const ROLES: readonly Role[] = ['Admin', 'ProjectManager', 'TeamMember'];
export const TASK_STATUSES: readonly TaskItemStatus[] = [
  'ToDo',
  'InProgress',
  'Completed',
  'Cancelled',
];
export const TASK_PRIORITIES: readonly TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

/** Default and maximum page size of every list endpoint (API Options). */
export const DEFAULT_PAGE_SIZE = 20;
export const PAGE_SIZE_OPTIONS: readonly number[] = [10, 20, 50, 100];

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreatedResponse {
  id: string;
}

export interface CurrentUserDto {
  id: string;
  fullName: string;
  email: string;
  role: Role;
}

export interface AuthResponse {
  accessToken: string;
  /** ISO date-time when the access token expires. */
  expiresAt: string;
  user: CurrentUserDto;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  fullName: string;
  password: string;
}

/** Person summary used by pickers (AssignableUserDto, ProjectOwnerDto, TaskAssigneeDto). */
export interface PersonDto {
  id: string;
  fullName: string;
  email: string;
}
