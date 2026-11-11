import {
  DEFAULT_PAGE_SIZE,
  SortDirection,
  TaskItemStatus,
  TaskPriority,
} from '../../core/models/api.models';
import { TaskListQuery, TaskSortField } from './models/task.models';

/** Task list state as the UI holds it (URL query or component state). */
export const TASK_FILTER_DEFAULTS = {
  search: '',
  status: '',
  priority: '',
  /** '' = anyone, 'me', 'unassigned', or a user id. */
  assignee: '',
  dueFrom: '',
  dueTo: '',
  sort: 'dueDate:Ascending',
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

export type TaskFilterState = typeof TASK_FILTER_DEFAULTS;

export const TASK_SORTS: readonly { value: string; label: string }[] = [
  { value: 'dueDate:Ascending', label: 'Due date, soonest' },
  { value: 'dueDate:Descending', label: 'Due date, latest' },
  { value: 'priority:Descending', label: 'Priority, highest' },
  { value: 'status:Ascending', label: 'Status' },
  { value: 'title:Ascending', label: 'Title A–Z' },
  { value: 'updatedAt:Descending', label: 'Recently updated' },
  { value: 'createdAt:Descending', label: 'Recently created' },
];

/** Maps the UI state to the API's list query (Unassigned and AssigneeId are separate filters). */
export function toTaskListQuery(state: TaskFilterState, me: string): TaskListQuery {
  const [sortBy, sortDirection] = state.sort.split(':') as [TaskSortField, SortDirection];
  const assignee = state.assignee;
  return {
    search: state.search,
    status: state.status as TaskItemStatus | '',
    priority: state.priority as TaskPriority | '',
    assigneeId: assignee === 'me' ? me : assignee === 'unassigned' ? '' : assignee,
    unassigned: assignee === 'unassigned',
    dueFrom: state.dueFrom,
    dueTo: state.dueTo,
    sortBy,
    sortDirection,
    page: state.page,
    pageSize: state.pageSize,
  };
}
