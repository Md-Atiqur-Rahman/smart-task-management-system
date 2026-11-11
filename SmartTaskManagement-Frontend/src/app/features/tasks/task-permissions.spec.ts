import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/config/api-base-url';
import { TasksApi } from './data-access/tasks-api.service';
import { canChangeStatus, canManageTask } from './task-permissions';

describe('task permissions (PRD 2-3, decision 1)', () => {
  it('canManageTask: Admin always', () => {
    expect(canManageTask('Admin', 'me', null)).toBe(true);
  });

  it('canManageTask: Project Manager only on own projects', () => {
    expect(canManageTask('ProjectManager', 'me', 'me')).toBe(true);
    expect(canManageTask('ProjectManager', 'me', 'other')).toBe(false);
    expect(canManageTask('ProjectManager', 'me', null)).toBe(false);
  });

  it('canManageTask: Team Member never', () => {
    expect(canManageTask('TeamMember', 'me', 'me')).toBe(false);
  });

  it('canChangeStatus: the assignee, whatever the role', () => {
    expect(canChangeStatus('TeamMember', 'me', 'me', null)).toBe(true);
    expect(canChangeStatus('ProjectManager', 'me', 'me', 'other')).toBe(true);
  });

  it('canChangeStatus: owning PM and Admin; not others', () => {
    expect(canChangeStatus('ProjectManager', 'me', 'someone', 'me')).toBe(true);
    expect(canChangeStatus('Admin', 'me', null, null)).toBe(true);
    expect(canChangeStatus('TeamMember', 'me', 'someone', null)).toBe(false);
    expect(canChangeStatus('ProjectManager', 'me', null, null)).toBe(false);
  });
});

describe('TasksApi', () => {
  it('assign_SendsNullToUnassign', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    const backend = TestBed.inject(HttpTestingController);

    TestBed.inject(TasksApi).assign('t1', null).subscribe();

    const req = backend.expectOne('https://api.test/api/tasks/t1/assignee');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ assigneeId: null });
    req.flush(null);
    backend.verify();
  });
});
