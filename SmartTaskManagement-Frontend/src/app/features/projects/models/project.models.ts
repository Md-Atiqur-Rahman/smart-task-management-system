import { PersonDto, SortDirection } from '../../../core/models/api.models';

/** Shapes from openapi.json (ProjectListItemDto, ProjectDetailsDto, ...). */
export interface ProjectListItemDto {
  id: string;
  name: string;
  ownerId: string;
  ownerName: string;
  createdAt: string;
  updatedAt: string | null;
  taskCount: number;
  completedTaskCount: number;
}

export interface ProjectTaskCountsDto {
  toDo: number;
  inProgress: number;
  completed: number;
  cancelled: number;
}

export interface ProjectDetailsDto {
  id: string;
  name: string;
  description: string | null;
  owner: PersonDto;
  createdAt: string;
  updatedAt: string | null;
  taskCounts: ProjectTaskCountsDto;
}

export interface CreateProjectRequest {
  name: string;
  description: string | null;
  /** Required for an Admin (a Project Manager); ignored for a Project Manager, who becomes the owner. */
  ownerId: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
}

/** Body of PUT /api/projects/{id}/owner (ChangeProjectOwnerRequest): the new owner, a Project Manager. */
export interface ChangeProjectOwnerRequest {
  ownerId: string;
}

export type ProjectSortField = 'name' | 'createdAt' | 'updatedAt';

export interface ProjectListQuery {
  search?: string;
  ownerId?: string;
  sortBy?: ProjectSortField;
  sortDirection?: SortDirection;
  page: number;
  pageSize: number;
}

/** API limits (Project.NameMaxLength, DescriptionMaxLength). */
export const PROJECT_NAME_MAX = 200;
export const PROJECT_DESCRIPTION_MAX = 2000;
