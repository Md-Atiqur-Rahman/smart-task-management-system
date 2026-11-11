import { TASK_FILTER_DEFAULTS, toTaskListQuery } from './task-query';

describe('toTaskListQuery', () => {
  it('sends the chosen project as projectId', () => {
    const query = toTaskListQuery({ ...TASK_FILTER_DEFAULTS, project: 'p-7' }, 'me-1');

    expect(query.projectId).toBe('p-7');
  });

  it('sends no project filter by default', () => {
    const query = toTaskListQuery(TASK_FILTER_DEFAULTS, 'me-1');

    expect(query.projectId).toBe('');
  });
});
