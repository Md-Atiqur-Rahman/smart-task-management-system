import { expect, test } from '@playwright/test';
import { adminFromEnv, newUser, pickPerson, register, signIn } from './helpers';

test.describe('authentication', () => {
  test('register, reload keeps the session, logout, wrong password, login', async ({ page }) => {
    const user = newUser('Member');

    await register(page, user);
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

    // The access token lives only in memory; a reload restores it from the HttpOnly refresh cookie.
    await page.reload();
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByText(user.fullName)).toBeVisible();

    await page.getByRole('button', { name: 'Log out' }).click();
    await expect(page).toHaveURL(/\/login$/);

    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password', { exact: true }).fill('WrongPassword1');
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('alert')).toHaveText('Email or password is incorrect.');

    await signIn(page, user.email, user.password);
    // A Team Member has no Users page and cannot create projects.
    await expect(page.getByRole('link', { name: 'Users' })).toHaveCount(0);
    await page.getByRole('link', { name: 'Projects' }).click();
    await expect(page.getByRole('button', { name: 'New project' })).toHaveCount(0);
  });
});

test.describe('projects, tasks and notifications (Admin)', () => {
  const admin = adminFromEnv();
  test.skip(!admin, 'Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to run the Admin tests.');

  test('create a project and an assigned task; the assignee gets a live notification', async ({
    browser,
  }) => {
    const credentials = admin!;
    const manager = newUser('Manager');
    const member = newUser('Member');
    const projectName = `E2E project ${Date.now()}`;
    const taskTitle = `E2E task ${Date.now()}`;

    // Two future users: one becomes the project's Project Manager, one the assignee.
    const managerContext = await browser.newContext();
    await register(await managerContext.newPage(), manager);
    await managerContext.close();

    const memberContext = await browser.newContext();
    const memberPage = await memberContext.newPage();
    await register(memberPage, member);
    await expect(memberPage.getByText('Live', { exact: true })).toBeVisible();

    // Admin, in a separate browser context (its own cookies).
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await signIn(adminPage, credentials.email, credentials.password);

    // Users: promote the manager (role change with confirmation).
    await adminPage.getByRole('link', { name: 'Users' }).click();
    await adminPage.getByLabel('Search users').fill(manager.email);
    const roleSelect = adminPage.getByLabel(`Role of ${manager.fullName}`);
    await expect(roleSelect).toBeVisible();
    await roleSelect.selectOption('ProjectManager');
    await adminPage.getByRole('button', { name: 'Change role' }).click();
    await expect(adminPage.getByText(`${manager.fullName} is now Project Manager.`)).toBeVisible();

    // Projects: create with the Project Manager as owner.
    await adminPage.getByRole('link', { name: 'Projects' }).click();
    await adminPage.getByRole('button', { name: 'New project' }).click();
    await adminPage.getByLabel('Name').fill(projectName);
    await adminPage.getByLabel('Description').fill('Created by the E2E suite.');
    await pickPerson(adminPage, 'Owner (Project Manager)', manager.email);
    await adminPage.getByRole('button', { name: 'Create project' }).click();
    await expect(adminPage).toHaveURL(/\/projects\/[^/]+$/);
    await expect(adminPage.getByRole('heading', { name: projectName })).toBeVisible();

    // Tasks: create and assign to the member.
    await adminPage.getByRole('button', { name: 'Add task' }).click();
    await adminPage.getByLabel('Title').fill(taskTitle);
    await pickPerson(adminPage, 'Assignee', member.email);
    await adminPage.getByRole('button', { name: 'Create task' }).click();
    await expect(adminPage).toHaveURL(/\/tasks\/[^/]+$/);
    await expect(adminPage.getByText(member.fullName)).toBeVisible();

    // The member's open page gets the notification live (SSE): badge and toast, no reload.
    await expect(memberPage.getByRole('button', { name: 'Notifications, 1 unread' })).toBeVisible();
    await expect(memberPage.getByRole('status').getByText(taskTitle)).toBeVisible();

    // Opening it from the bell goes to the task and marks it read.
    await memberPage.getByRole('button', { name: 'Notifications, 1 unread' }).click();
    await memberPage.getByRole('button', { name: new RegExp(taskTitle) }).click();
    await expect(memberPage).toHaveURL(/\/tasks\/[^/]+$/);
    await expect(memberPage.getByRole('heading', { name: taskTitle })).toBeVisible();
    await expect(
      memberPage.getByRole('button', { name: 'Notifications, no unread' }),
    ).toBeVisible();

    await adminContext.close();
    await memberContext.close();
  });
});
