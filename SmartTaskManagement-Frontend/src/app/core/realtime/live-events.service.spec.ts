import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { AuthStore } from '../auth/auth.store';
import { API_BASE_URL } from '../config/api-base-url';
import { NotificationStore } from '../notifications/notification.store';
import { DashboardRefresh } from './dashboard-refresh';
import { LiveEventsService } from './live-events.service';

const API = 'https://api.test';

/** A fetch Response whose body streams the given chunks and then ends. */
function streamResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk));
      controller.close();
    },
  });
  return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
}

describe('LiveEventsService (fetch + ReadableStream SSE client)', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let live: LiveEventsService;

  beforeEach(() => {
    vi.useFakeTimers();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: { refresh: () => of(true) } },
      ],
    });
    TestBed.inject(AuthStore).setSession({
      accessToken: 'tok',
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
      user: { id: 'u1', fullName: 'Test', email: 't@example.com', role: 'TeamMember' },
    });
    live = TestBed.inject(LiveEventsService);
  });

  afterEach(() => {
    live.stop();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('sends the bearer header, never the token in the URL', async () => {
    fetchMock.mockResolvedValue(streamResponse([]));
    live.start();
    await vi.advanceTimersByTimeAsync(0);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${API}/api/notifications/stream`);
    expect(url).not.toContain('tok');
    expect((init.headers as Record<string, string>)['Authorization']).toBe('Bearer tok');
  });

  it('routes notification and dashboard-changed events', async () => {
    const notifications = TestBed.inject(NotificationStore);
    const refresh = TestBed.inject(DashboardRefresh);
    const onLive = vi.spyOn(notifications, 'onLive');
    const notifyChanged = vi.spyOn(refresh, 'notifyChanged');
    fetchMock.mockResolvedValueOnce(
      streamResponse([
        'event: notification\ndata: {"id":"n1","type":"TaskAssigned","message":"m","taskId":"t1","projectId":"p1","createdAt":"2026-11-11T12:00:00Z","isRead":false}\n\n',
        'event: dashboard-changed\ndata: \n\nevent: heartbeat\ndata: \n\n',
      ]),
    );
    fetchMock.mockReturnValue(new Promise(() => undefined));

    live.start();
    await vi.advanceTimersByTimeAsync(0);

    expect(onLive).toHaveBeenCalledWith(expect.objectContaining({ id: 'n1' }));
    expect(notifyChanged).toHaveBeenCalledTimes(1);
  });

  it('reconnects with backoff after the stream ends, and stops on stop()', async () => {
    fetchMock.mockResolvedValueOnce(streamResponse([]));
    fetchMock.mockResolvedValueOnce(streamResponse([]));
    fetchMock.mockReturnValue(new Promise(() => undefined));

    live.start();
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(live.status()).toBe('reconnecting');

    await vi.advanceTimersByTimeAsync(2_000);
    expect(fetchMock).toHaveBeenCalledTimes(2);

    live.stop();
    await vi.advanceTimersByTimeAsync(60_000);
    expect(fetchMock.mock.calls.length).toBeLessThanOrEqual(3);
    expect(live.status()).toBe('off');
  });
});
