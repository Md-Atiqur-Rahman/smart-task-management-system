import { dueLabel, utcToday } from './date-utils';

describe('date utils (the API uses the UTC date, PRD 9)', () => {
  it('utcToday returns the UTC calendar date', () => {
    expect(utcToday(new Date('2026-11-11T23:30:00-05:00'))).toBe('2026-11-12');
    expect(utcToday(new Date('2026-11-11T00:10:00+06:00'))).toBe('2026-11-10');
  });

  it.each([
    ['2026-11-10', 'Overdue'],
    ['2026-11-11', 'Today'],
    ['2026-11-12', 'Tomorrow'],
    ['2026-11-20', null],
    [null, null],
  ])('dueLabel(%s) on 2026-11-11 -> %s', (due, expected) => {
    expect(dueLabel(due, '2026-11-11')).toBe(expected);
  });

  it('a completed or cancelled task is never overdue', () => {
    expect(dueLabel('2026-11-01', '2026-11-11', 'Completed')).toBeNull();
    expect(dueLabel('2026-11-01', '2026-11-11', 'Cancelled')).toBeNull();
  });
});
