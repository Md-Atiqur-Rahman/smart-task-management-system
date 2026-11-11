import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Two-column layout of the sign-in and register pages (stacks on phones). */
@Component({
  selector: 'app-auth-layout',
  template: `
    <div class="auth">
      <section class="intro" aria-label="About SmartTask">
        <span class="brand">SmartTask</span>
        <div class="pitch">
          <h2>Plan projects. Ship tasks. Keep everyone in step.</h2>
          <p>
            A secure task manager for teams, with role-based access, a live dashboard and instant
            notifications when work is assigned to you.
          </p>
        </div>
        <p class="stack">ASP.NET Core 10 · Angular · SQL Server</p>
      </section>
      <main class="form-side">
        <ng-content />
      </main>
    </div>
  `,
  styles: `
    .auth {
      display: flex;
      flex-wrap: wrap;
      min-height: 100vh;
    }
    .intro {
      flex: 1 1 420px;
      background: var(--color-sidebar);
      color: #e2e8f0;
      padding: 56px 48px;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 32px;
    }
    .brand {
      font: 700 22px var(--font-heading);
      color: #fff;
    }
    .pitch {
      max-width: 460px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .pitch h2 {
      font-size: 36px;
      line-height: 1.1;
      color: #fff;
    }
    .pitch p {
      font-size: 16px;
      line-height: 1.6;
      color: #cbd5e1;
    }
    .stack {
      font-size: 13px;
      color: #94a3b8;
    }
    .form-side {
      flex: 1 1 420px;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 32px 16px;
    }
    @media (max-width: 767px) {
      .intro {
        padding: 24px 16px;
        flex-basis: 100%;
      }
      .pitch h2 {
        font-size: 24px;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayoutComponent {}
