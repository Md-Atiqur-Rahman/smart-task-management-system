import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PersonDto } from '../core/models/api.models';
import { UserPickerComponent } from './user-picker.component';

const person: PersonDto = { id: 'u-1', fullName: 'Rahim Uddin', email: 'rahim@example.com' };

describe('UserPickerComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('clears the shown name when the parent no longer has that person selected', async () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(UserPickerComponent);
    fixture.componentRef.setInput('label', 'Choose a person');
    fixture.componentRef.setInput('search', () =>
      of({ items: [person], page: 1, pageSize: 10, totalCount: 1 }),
    );
    fixture.componentRef.setInput('selected', null);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input')!;

    input.value = 'rah';
    input.dispatchEvent(new Event('input'));
    await vi.advanceTimersByTimeAsync(300);
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.componentRef.setInput('selected', person.id);
    fixture.detectChanges();
    expect(input.value).toBe('Rahim Uddin');

    // "Clear filters" or another assignee choice: the filter is no longer this person.
    fixture.componentRef.setInput('selected', null);
    fixture.detectChanges();

    expect(input.value).toBe('');
  });
});
