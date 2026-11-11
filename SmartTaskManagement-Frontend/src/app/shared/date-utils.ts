import { TaskItemStatus } from '../core/models/api.models';

export type DueLabel = 'Overdue' | 'Today' | 'Tomorrow';

/** Today's date in UTC as YYYY-MM-DD: "today" for due dates is the server's UTC date (PRD 9). */
export function utcToday(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

function addDays(isoDate: string, days: number): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** Label for a date-only due date; only pending tasks (To Do, In Progress) can be overdue. */
export function dueLabel(
  due: string | null,
  today: string,
  status: TaskItemStatus = 'ToDo',
): DueLabel | null {
  if (!due || status === 'Completed' || status === 'Cancelled') {
    return null;
  }
  if (due < today) return 'Overdue';
  if (due === today) return 'Today';
  if (due === addDays(today, 1)) return 'Tomorrow';
  return null;
}

/** Shows a date-only value without time-zone shifts (e.g. "Nov 12, 2026"). */
export function formatDateOnly(isoDate: string | null): string {
  if (!isoDate) return '—';
  return new Date(`${isoDate}T00:00:00Z`).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}
