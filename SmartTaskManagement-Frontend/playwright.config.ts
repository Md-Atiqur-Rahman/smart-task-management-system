import { defineConfig } from '@playwright/test';

/**
 * End-to-end tests (frontend-design.md section 13): local only, against the
 * real API (https://localhost:7254, started separately) and the HTTPS dev
 * server. Uses the installed Microsoft Edge by default (no browser download);
 * set E2E_CHANNEL=chrome or run `npx playwright install chromium` and set
 * E2E_CHANNEL=chromium to use another browser.
 * Admin tests need E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD in the environment
 * (never committed, never printed); without them those tests are skipped.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'https://localhost:4200',
    channel: process.env['E2E_CHANNEL'] ?? 'msedge',
    viewport: { width: 1280, height: 860 },
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm start',
    // Wait for the port, not an HTTPS request: Node's readiness check does not use
    // the Windows certificate store, and TLS checks are never switched off here.
    port: 4200,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
