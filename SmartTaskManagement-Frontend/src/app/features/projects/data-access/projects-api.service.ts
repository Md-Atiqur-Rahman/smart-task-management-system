import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { CreatedResponse, PagedResult } from '../../../core/models/api.models';
import { toApiParams } from '../../../shared/list-query';
import {
  ChangeProjectOwnerRequest,
  CreateProjectRequest,
  ProjectDetailsDto,
  ProjectListItemDto,
  ProjectListQuery,
  UpdateProjectRequest,
} from '../models/project.models';

/** /api/projects, including the owner change (PUT /owner, Admin only). */
@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/api/projects`;

  list(query: ProjectListQuery): Observable<PagedResult<ProjectListItemDto>> {
    return this.http.get<PagedResult<ProjectListItemDto>>(this.url, {
      params: toApiParams({ ...query }),
    });
  }

  get(id: string): Observable<ProjectDetailsDto> {
    return this.http.get<ProjectDetailsDto>(`${this.url}/${encodeURIComponent(id)}`);
  }

  create(body: CreateProjectRequest): Observable<CreatedResponse> {
    return this.http.post<CreatedResponse>(this.url, body);
  }

  update(id: string, body: UpdateProjectRequest): Observable<void> {
    return this.http.put<void>(`${this.url}/${encodeURIComponent(id)}`, body);
  }

  /** Gives the project to another Project Manager (Admin only; prototype D08). */
  changeOwner(id: string, ownerId: string): Observable<void> {
    const body: ChangeProjectOwnerRequest = { ownerId };
    return this.http.put<void>(`${this.url}/${encodeURIComponent(id)}/owner`, body);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }
}
