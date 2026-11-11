import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, timeout } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { CreatedResponse, PagedResult, TaskItemStatus } from '../../../core/models/api.models';
import { toApiParams } from '../../../shared/list-query';
import {
  CreateTaskRequest,
  TaskDetailsDto,
  TaskListItemDto,
  TaskListQuery,
  UpdateTaskRequest,
} from '../models/task.models';

const IMPROVE_TIMEOUT_MS = 30_000;

/** /api/tasks and /api/projects/{projectId}/tasks. */
@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly url = `${this.baseUrl}/api/tasks`;

  list(query: TaskListQuery): Observable<PagedResult<TaskListItemDto>> {
    return this.http.get<PagedResult<TaskListItemDto>>(this.url, {
      params: toApiParams({ ...query }),
    });
  }

  listForProject(
    projectId: string,
    query: TaskListQuery,
  ): Observable<PagedResult<TaskListItemDto>> {
    return this.http.get<PagedResult<TaskListItemDto>>(this.projectTasksUrl(projectId), {
      params: toApiParams({ ...query, projectId: undefined }),
    });
  }

  get(id: string): Observable<TaskDetailsDto> {
    return this.http.get<TaskDetailsDto>(this.taskUrl(id));
  }

  create(projectId: string, body: CreateTaskRequest): Observable<CreatedResponse> {
    return this.http.post<CreatedResponse>(this.projectTasksUrl(projectId), body);
  }

  update(id: string, body: UpdateTaskRequest): Observable<void> {
    return this.http.put<void>(this.taskUrl(id), body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(this.taskUrl(id));
  }

  changeStatus(id: string, status: TaskItemStatus): Observable<void> {
    return this.http.put<void>(`${this.taskUrl(id)}/status`, { status });
  }

  /** null unassigns. */
  assign(id: string, assigneeId: string | null): Observable<void> {
    return this.http.put<void>(`${this.taskUrl(id)}/assignee`, { assigneeId });
  }

  /**
   * Suggests an improved description (POST /api/tasks/description/improve, Admin and
   * Project Manager). Nothing is saved; the user decides whether to use the text.
   * The API answers within 25 s; the client gives up after 30 s (AI design 3).
   */
  improveDescription(title: string | null, description: string): Observable<string> {
    return this.http
      .post<{ description: string }>(`${this.url}/description/improve`, { title, description })
      .pipe(
        timeout(IMPROVE_TIMEOUT_MS),
        map((result) => result.description),
      );
  }

  private taskUrl(id: string): string {
    return `${this.url}/${encodeURIComponent(id)}`;
  }

  private projectTasksUrl(projectId: string): string {
    return `${this.baseUrl}/api/projects/${encodeURIComponent(projectId)}/tasks`;
  }
}
