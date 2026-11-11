import { convertToParamMap } from '@angular/router';
import { statusLabel, priorityLabel, roleLabel } from './labels';
import { readListQuery, toApiParams, toQueryParams, totalPages } from './list-query';

describe('labels', () => {
  it('maps every API enum string to its display label', () => {
    expect(statusLabel('ToDo')).toBe('To Do');
    expect(statusLabel('InProgress')).toBe('In Progress');
    expect(statusLabel('Completed')).toBe('Completed');
    expect(statusLabel('Cancelled')).toBe('Cancelled');
    expect(priorityLabel('Critical')).toBe('Critical');
    expect(roleLabel('ProjectManager')).toBe('Project Manager');
    expect(roleLabel('TeamMember')).toBe('Team Member');
    expect(roleLabel('Admin')).toBe('Admin');
  });
});

describe('list query helpers', () => {
  const defaults = { search: '', status: '', page: 1, pageSize: 20, unassigned: false };

  it('toQueryParams drops empty values and defaults', () => {
    expect(
      toQueryParams(
        { search: 'pay', status: '', page: 1, pageSize: 50, unassigned: false },
        defaults,
      ),
    ).toEqual({ search: 'pay', pageSize: '50' });
  });

  it('readListQuery round-trips toQueryParams', () => {
    const query = { search: 'pay', status: 'ToDo', page: 3, pageSize: 50, unassigned: true };
    const params = convertToParamMap(toQueryParams(query, defaults));

    expect(readListQuery(params, defaults)).toEqual(query);
  });

  it('readListQuery falls back to defaults for bad numbers', () => {
    const params = convertToParamMap({ page: 'abc', pageSize: '-5' });

    expect(readListQuery(params, defaults)).toEqual(defaults);
  });

  it('toApiParams uses the API PascalCase names and skips empty values', () => {
    const params = toApiParams({ search: 'pay', ownerId: '', page: 2, unassigned: true });

    expect(params.toString()).toBe('Search=pay&Page=2&Unassigned=true');
  });

  it('totalPages rounds up and is at least 1', () => {
    expect(totalPages(0, 20)).toBe(1);
    expect(totalPages(41, 20)).toBe(3);
  });
});
