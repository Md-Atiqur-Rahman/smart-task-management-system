import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { UsersApi } from '../../users/data-access/users-api.service';
import { ProjectsApi } from './projects-api.service';

const API = 'https://api.test';

describe('ProjectsApi', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('list_SendsPascalCaseQueryNames_AndOmitsEmptyOnes', () => {
    TestBed.inject(ProjectsApi)
      .list({
        search: 'portal',
        ownerId: '',
        sortBy: 'name',
        sortDirection: 'Ascending',
        page: 2,
        pageSize: 20,
      })
      .subscribe();

    const req = backend.expectOne((r) => r.url === `${API}/api/projects`);
    expect(req.request.params.toString()).toBe(
      'Search=portal&SortBy=name&SortDirection=Ascending&Page=2&PageSize=20',
    );
    req.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('create_PostsTheBody_AndReturnsTheNewId', () => {
    let id = '';
    TestBed.inject(ProjectsApi)
      .create({ name: 'Supplier Portal', description: null, ownerId: 'pm-1' })
      .subscribe((created) => (id = created.id));

    const req = backend.expectOne(`${API}/api/projects`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Supplier Portal',
      description: null,
      ownerId: 'pm-1',
    });
    req.flush({ id: 'p-9' });
    expect(id).toBe('p-9');
  });

  it('UsersApi_projectManagers_FiltersByRole', () => {
    TestBed.inject(UsersApi).projectManagers('sad').subscribe();

    const req = backend.expectOne((r) => r.url === `${API}/api/users`);
    expect(req.request.params.toString()).toBe('Search=sad&Role=ProjectManager&Page=1&PageSize=10');
    req.flush({ items: [], page: 1, pageSize: 10, totalCount: 0 });
  });
});
