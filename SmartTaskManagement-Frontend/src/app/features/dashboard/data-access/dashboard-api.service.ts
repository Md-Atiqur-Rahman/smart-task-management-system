import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { TaskItemStatus, TaskPriority } from '../../../core/models/api.models';

/** DashboardSummaryDto (openapi.json): every status and priority key is always present. */
export interface DashboardSummaryDto {
  totalProjects: number;
  totalTasks: number;
  tasksByStatus: { toDo: number; inProgress: number; completed: number; cancelled: number };
  tasksByPriority: { low: number; medium: number; high: number; critical: number };
  completedTasks: number;
  pendingTasks: number;
  overdueTasks: number;
}

/** UpcomingTaskDto: pending tasks due from today to today + the configured window. */
export interface UpcomingTaskDto {
  id: string;
  title: string;
  dueDate: string;
  status: TaskItemStatus;
  priority: TaskPriority;
  projectId: string;
  projectName: string;
  assigneeId: string | null;
  assigneeName: string | null;
}

/** /api/dashboard: two calls so the cards load first (PRD 9). */
@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/api/dashboard`;

  summary(): Observable<DashboardSummaryDto> {
    return this.http.get<DashboardSummaryDto>(`${this.url}/summary`);
  }

  upcoming(): Observable<UpcomingTaskDto[]> {
    return this.http.get<UpcomingTaskDto[]>(`${this.url}/upcoming`);
  }
}
