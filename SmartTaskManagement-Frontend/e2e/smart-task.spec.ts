import { expect, test } from '@playwright/test';
import {
  adminFromEnv,
  createProject,
  newUser,
  openFromMenu,
  pickPerson,
  promoteToProjectManager,
  register,
  signIn,
} from './helpers';

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
    await promoteToProjectManager(adminPage, manager);

    // Projects: create with the Project Manager as owner.
    await createProject(adminPage, projectName, manager);

    // Tasks: create and assign to the member.
    await adminPage.getByRole('button', { name: 'Add task' }).click();
    const taskDialog = adminPage.getByRole('dialog', { name: 'New task' });
    await taskDialog.getByLabel('Title').fill(taskTitle);
    await pickPerson(taskDialog, 'Assignee', member.email);
    await taskDialog.getByRole('button', { name: 'Create task' }).click();
    await expect(adminPage).toHaveURL(/\/tasks\/[^/]+$/);
    await expect(
      adminPage.getByRole('complementary', { name: 'Task properties' }).getByText(member.fullName),
    ).toBeVisible();

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

test.describe('owner change and project filter (Admin)', () => {
  const admin = adminFromEnv();
  test.skip(!admin, 'Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to run the Admin tests.');

  test('the Admin gives a project to another Project Manager; /tasks filters by project', async ({
    browser,
  }) => {
    const credentials = admin!;
    const first = newUser('Owner');
    const second = newUser('NewOwner');
    const projectName = `E2E owner project ${Date.now()}`;
    const taskTitle = `E2E filtered task ${Date.now()}`;

    for (const user of [first, second]) {
      const context = await browser.newContext();
      await register(await context.newPage(), user);
      await context.close();
    }

    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();
    await signIn(adminPage, credentials.email, credentials.password);
    await promoteToProjectManager(adminPage, first);
    await promoteToProjectManager(adminPage, second);
    await createProject(adminPage, projectName, first);

    // Change owner: the dialog shows the current owner; saving shows the new one on the page.
    await adminPage.getByRole('button', { name: 'Change owner' }).click();
    const ownerDialog = adminPage.getByRole('dialog', { name: 'Change owner' });
    await expect(ownerDialog.getByText(first.fullName)).toBeVisible();
    await pickPerson(ownerDialog, 'New owner (Project Manager)', second.email);
    await ownerDialog.getByRole('button', { name: `Make ${second.fullName} the owner` }).click();
    await expect(ownerDialog).toHaveCount(0);
    await expect(adminPage.getByRole('definition').getByText(second.fullName)).toBeVisible();

    // A task in this project, then the project filter on /tasks shows only this project's task.
    await adminPage.getByRole('button', { name: 'Add task' }).click();
    const taskDialog = adminPage.getByRole('dialog', { name: 'New task' });
    await taskDialog.getByLabel('Title').fill(taskTitle);
    await taskDialog.getByRole('button', { name: 'Create task' }).click();
    await expect(adminPage).toHaveURL(/\/tasks\/[^/]+$/);

    await openFromMenu(adminPage, 'Tasks');
    await adminPage.getByLabel('Project', { exact: true }).selectOption({ label: projectName });
    await expect(adminPage).toHaveURL(/[?&]project=/);
    const rows = adminPage.getByRole('table').locator('tbody tr');
    await expect(rows).toHaveCount(1);
    await expect(rows.first().getByRole('link', { name: taskTitle })).toBeVisible();

    await adminContext.close();
  });
});

test.describe('Improve with AI (real Gemini call)', () => {
  const admin = adminFromEnv();
  test.skip(
    !admin || process.env['E2E_AI'] !== '1',
    'Set E2E_AI=1 (and the Admin variables) to call the real AI endpoint.',
  );

  test('suggests a description and replaces the text only when the user accepts it', async ({
    page,
  }) => {
    const credentials = admin!;
    await signIn(page, credentials.email, credentials.password);

    // Any existing task (the Admin sees all); the dialog is cancelled, nothing is saved.
    await page.getByRole('link', { name: 'Tasks' }).click();
    await page.getByRole('table').getByRole('link').first().click();
    await expect(page).toHaveURL(/\/tasks\/[^/]+$/);
    await page.getByRole('button', { name: 'Edit' }).click();

    const dialog = page.getByRole('dialog', { name: 'Edit task' });
    await expect(
      dialog.getByText('sends the title and description to Google Gemini'),
    ).toBeVisible();
    await dialog.getByLabel('Description').fill('fix the login bug');
    await dialog.getByRole('button', { name: 'Improve with AI' }).click();

    const suggestion = dialog.getByRole('region', { name: 'AI suggestion' });
    await expect(suggestion).toBeVisible({ timeout: 35_000 });
    // The user's text is unchanged until they accept the suggestion.
    await expect(dialog.getByLabel('Description')).toHaveValue('fix the login bug');

    await suggestion.getByRole('button', { name: 'Use this description' }).click();
    await expect(dialog.getByLabel('Description')).not.toHaveValue('fix the login bug');
    await expect(suggestion).toHaveCount(0);

    await dialog.getByRole('button', { name: 'Cancel' }).click();
  });
});
