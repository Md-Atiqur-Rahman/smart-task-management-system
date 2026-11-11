import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ModalComponent } from './modal.component';

@Component({
  imports: [ModalComponent],
  template: `<app-modal heading="Edit task"
    ><button type="button" id="inside">Save</button></app-modal
  >`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class HostComponent {}

describe('ModalComponent', () => {
  it('returns focus to the element that opened it when it is removed', async () => {
    // jsdom has no <dialog>.showModal; the modal calls it after the first render.
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    const opener = document.createElement('button');
    document.body.appendChild(opener);
    opener.focus();

    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    document.body.appendChild(fixture.nativeElement as HTMLElement);
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('#inside')!.focus();
    expect(document.activeElement?.id).toBe('inside');

    // @if removes the modal (for example after Save) without closing the <dialog>.
    fixture.destroy();

    expect(document.activeElement).toBe(opener);
    opener.remove();
  });
});
