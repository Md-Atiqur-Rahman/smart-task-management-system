import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { errorInterceptor } from '../../../core/auth/error.interceptor';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { TaskFormDialogComponent } from './task-form.dialog';

const API = 'https://api.test';
const IMPROVE = `${API}/api/tasks/description/improve`;

describe('TaskFormDialogComponent: Improve with AI', () => {
  let backend: HttpTestingController;

  async function setup(description: string) {
    // jsdom has no <dialog>.showModal; the modal calls it after the first render.
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    TestBed.configureTestingModule({
      imports: [TaskFormDialogComponent],
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TaskFormDialogComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const textarea = host.querySelector<HTMLTextAreaElement>('#task-description')!;
    textarea.value = description;
    textarea.dispatchEvent(new Event('input'));
    const title = host.querySelector<HTMLInputElement>('#task-title')!;
    title.value = 'Login';
    title.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, host, textarea };
  }

  function button(host: HTMLElement, text: string): HTMLButtonElement {
    const found = [...host.querySelectorAll('button')].find((b) => b.textContent?.includes(text));
    if (!found) throw new Error(`button "${text}" not found`);
    return found;
  }

  afterEach(() => backend.verify());

  it('is disabled while the description is empty', async () => {
    const { host } = await setup('');

    expect(button(host, 'Improve with AI').disabled).toBe(true);
  });

  it('shows the suggestion and replaces the description only when the user accepts it', async () => {
    const { fixture, host, textarea } = await setup('fix the login bug');

    button(host, 'Improve with AI').click();
    const req = backend.expectOne(IMPROVE);
    expect(req.request.body).toEqual({ title: 'Login', description: 'fix the login bug' });
    req.flush({ description: 'Goal: Fix the login bug.' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(host.textContent).toContain('Goal: Fix the login bug.');
    expect(textarea.value).toBe('fix the login bug');

    button(host, 'Use this description').click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(textarea.value).toBe('Goal: Fix the login bug.');
    expect(host.textContent).not.toContain('AI suggestion');
  });

  it('keeps the text and explains a rejected text (ai.rejected)', async () => {
    const { fixture, host, textarea } = await setup('something odd');

    button(host, 'Improve with AI').click();
    backend
      .expectOne(IMPROVE)
      .flush({ status: 400, errorCode: 'ai.rejected' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(host.textContent).toContain(
      'The AI service declined this text. Rewrite it and try again.',
    );
    expect(textarea.value).toBe('something odd');
  });
});
