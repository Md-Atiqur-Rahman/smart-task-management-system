import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { PagedResult, PersonDto, Role, SortDirection } from '../../../core/models/api.models';
import { toApiParams } from '../../../shared/list-query';

/** UserDto from openapi.json (Admin list). */
export interface UserDto {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  createdAt: string;
}

export type UserSortField = 'fullName' | 'email' | 'role' | 'createdAt';

export interface UserListQuery {
  search?: string;
  role?: Role | '';
  sortBy?: UserSortField;
  sortDirection?: SortDirection;
  page: number;
  pageSize: number;
}

const PICKER_PAGE_SIZE = 10;

/** /api/users: Admin list and role change; assignable search for Admin and PM. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/api/users`;

  list(query: UserListQuery): Observable<PagedResult<UserDto>> {
    return this.http.get<PagedResult<UserDto>>(this.url, { params: toApiParams({ ...query }) });
  }

  /** Admin only: Project Managers for owner pickers and the owner filter. */
  projectManagers(search: string): Observable<PagedResult<UserDto>> {
    return this.list({ search, role: 'ProjectManager', page: 1, pageSize: PICKER_PAGE_SIZE });
  }

  /** Admin: all users; Project Manager: all Team Members and themselves (auth-design.md). */
  assignable(search: string, page = 1): Observable<PagedResult<PersonDto>> {
    return this.http.get<PagedResult<PersonDto>>(`${this.url}/assignable`, {
      params: toApiParams({ search, page, pageSize: PICKER_PAGE_SIZE }),
    });
  }

  changeRole(id: string, role: Role): Observable<void> {
    return this.http.put<void>(`${this.url}/${encodeURIComponent(id)}/role`, { role });
  }
}
