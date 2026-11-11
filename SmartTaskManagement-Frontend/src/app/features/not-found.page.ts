import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** 404 page (prototype D10). Same wording whether the item is missing or hidden from you. */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: `
    <main class="not-found">
      <p class="code">404</p>
      <h1>Not found</h1>
      <p class="muted">
        This page does not exist, was deleted, or you no longer have access to it.
      </p>
      <a class="btn" routerLink="/dashboard">Back to dashboard</a>
    </main>
  `,
  styles: `
    .not-found {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 16px;
      text-align: center;
    }
    .code {
      font: 700 48px var(--font-heading);
      color: var(--color-primary);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPage {}
