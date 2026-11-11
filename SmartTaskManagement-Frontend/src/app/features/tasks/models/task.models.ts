import {
  PersonDto,
  SortDirection,
  TaskItemStatus,
  TaskPriority,
} from '../../../core/models/api.models';

/** Shapes from openapi.json (TaskListItemDto, TaskDetailsDto, ...). */
export interface TaskListItemDto {
  id: string;
  title: string;
  status: TaskItemStatus;
  priority: TaskPriority;
  /** Date only, YYYY-MM-DD. */
  dueDate: string | null;
  assigneeId: string | null;
  assigneeName: string | null;
  projectId: string;
  projectName: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface TaskDetailsDto {
  id: string;
  title: string;
  description: string | null;
  status: TaskItemStatus;
  priority: TaskPriority;
  dueDate: string | null;
  assignee: PersonDto | null;
  project: { id: string; name: string };
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
  assigneeId: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
}

export type TaskSortField = 'title' | 'priority' | 'dueDate' | 'status' | 'createdAt' | 'updatedAt';

export interface TaskListQuery {
  search?: string;
  projectId?: string;
  status?: TaskItemStatus | '';
  priority?: TaskPriority | '';
  assigneeId?: string;
  unassigned?: boolean;
  dueFrom?: string;
  dueTo?: string;
  sortBy?: TaskSortField;
  sortDirection?: SortDirection;
  page: number;
  pageSize: number;
}

/** API limits (TaskItem.TitleMaxLength, DescriptionMaxLength). */
export const TASK_TITLE_MAX = 200;
export const TASK_DESCRIPTION_MAX = 4000;
