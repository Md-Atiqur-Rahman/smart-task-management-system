import { ApiProblem } from './api-problem';

/** User-facing texts per API errorCode (frontend-design.md section 7). */
const BY_CODE: Readonly<Record<string, string>> = {
  'auth.invalid_credentials': 'Email or password is incorrect.',
  'auth.origin_not_allowed': "This app's address is not allowed by the server (CORS settings).",
  'auth.unavailable': 'The service is temporarily unavailable. Please try again.',
  'auth.token_revoked': 'Your session has ended. Please sign in again.',
  'auth.invalid_refresh_token': 'Your session has ended. Please sign in again.',
  'validation.failed': 'Please correct the highlighted fields.',
  'concurrency.conflict': 'Someone else changed this. Reload to see the latest version.',
  'users.owns_projects':
    'This user still owns projects. Give them to another Project Manager first.',
  'users.last_admin': 'There must be at least one Admin.',
  'users.email_taken': 'An account with this email already exists.',
  'users.not_found': 'This user no longer exists.',
  'persistence.duplicate_key': 'This already exists.',
  'projects.invalid_owner': 'The owner must be a Project Manager.',
  'projects.owner_not_allowed': 'The owner must be a Project Manager.',
  'projects.not_found': 'This project no longer exists or you no longer have access.',
  'tasks.invalid_assignee': "This person can't be assigned to this task.",
  'tasks.assignee_not_allowed': "This person can't be assigned to this task.",
  'tasks.not_found': 'This task no longer exists or you no longer have access.',
  'notifications.not_found': 'This notification no longer exists.',
};

const BY_STATUS: Readonly<Record<number, string>> = {
  0: "Can't reach the server. Check your connection.",
  400: 'Please correct the highlighted fields.',
  401: 'Your session has ended. Please sign in again.',
  403: "You don't have permission to do that.",
  404: 'This item no longer exists or you no longer have access.',
  409: 'Someone else changed this. Reload to see the latest version.',
  429: 'Too many requests. Wait a minute and try again.',
  503: 'The service is temporarily unavailable. Please try again.',
};

const GENERIC = 'Something went wrong. Please try again.';

export function messageFor(problem: ApiProblem): string {
  if (problem.errorCode && BY_CODE[problem.errorCode]) {
    return BY_CODE[problem.errorCode];
  }
  return BY_STATUS[problem.status] ?? GENERIC;
}
