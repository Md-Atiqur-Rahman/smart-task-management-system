import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-base-url';
import { NotificationDto, NotificationStore } from './notification.store';

const API = 'https://api.test';

function dto(id: string, isRead = false): NotificationDto {
  return {
    id,
    type: 'TaskAssigned',
    message: `Message ${id}`,
    taskId: 't1',
    projectId: 'p1',
    createdAt: '2026-11-11T12:00:00Z',
    isRead,
  };
}

describe('NotificationStore', () => {
  let store: NotificationStore;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(NotificationStore);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('loadCount_SetsTheUnreadBadge', () => {
    store.loadCount();
    backend.expectOne(`${API}/api/notifications/unread-count`).flush({ count: 3 });

    expect(store.unread()).toBe(3);
  });

  it('onLive_AddsTheItemOnTop_AndIncrementsTheBadge', () => {
    store.loadList();
    backend.expectOne(`${API}/api/notifications`).flush([dto('old', true)]);

    store.onLive(dto('new'));

    expect(store.unread()).toBe(1);
    expect(store.items().map((n) => n.id)).toEqual(['new', 'old']);
  });

  it('markRead_MarksTheItem_AndDecrementsTheBadge', () => {
    store.onLive(dto('n1'));
    store.onLive(dto('n2'));

    store.markRead('n1');
    backend.expectOne(`${API}/api/notifications/n1/read`).flush(null);

    expect(store.unread()).toBe(1);
    expect(store.items().find((n) => n.id === 'n1')?.isRead).toBe(true);
  });

  it('markAllRead_ClearsTheBadge', () => {
    store.onLive(dto('n1'));
    store.markAllRead();
    backend.expectOne(`${API}/api/notifications/read-all`).flush(null);

    expect(store.unread()).toBe(0);
    expect(store.items().every((n) => n.isRead)).toBe(true);
  });

  it('reset_ClearsEverything', () => {
    store.onLive(dto('n1'));
    store.reset();

    expect(store.unread()).toBe(0);
    expect(store.items()).toEqual([]);
  });
});
