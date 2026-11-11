import { Page, expect } from '@playwright/test';

export interface TestUser {
  fullName: string;
  email: string;
  password: string;
}

/** A unique, throw-away account (soft delete only, so test data stays in the local database). */
export function newUser(label: string): TestUser {
  const id = `${Date.now()}${Math.floor(Math.random() * 1000)}`;
  return {
    fullName: `E2E ${label} ${id}`,
    email: `e2e.${label.toLowerCase()}.${id}@example.com`,
    password: `E2e${id}x`,
  };
}

export function adminFromEnv(): { email: string; password: string } | null {
  const email = process.env['E2E_ADMIN_EMAIL'];
  const password = process.env['E2E_ADMIN_PASSWORD'];
  return email && password ? { email, password } : null;
}

export async function register(page: Page, user: TestUser): Promise<void> {
  await page.goto('/register');
  await page.getByLabel('Full name').fill(user.fullName);
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password', { exact: true }).fill(user.password);
  await page.getByLabel('Confirm password').fill(user.password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

/** Types into an ARIA combobox user picker and picks the first match. */
export async function pickPerson(page: Page, label: string, search: string): Promise<void> {
  const box = page.getByRole('combobox', { name: label });
  await box.fill(search);
  // Scope to this picker's own listbox (aria-label = label); the page behind a
  // dialog has native <select> options too.
  const firstOption = page.getByRole('listbox', { name: label }).getByRole('option').first();
  await expect(firstOption).toContainText(search);
  await box.press('Enter');
}
