import { expect, test } from '@playwright/test';

test.describe('app shell and demo auth', () => {
  test.beforeEach(async ({ page }) => {
    // Start each test from a fresh browser state — but only on the first load, so reloads keep it.
    await page.addInitScript(() => {
      if (window.sessionStorage.getItem('e2e-started') === null) {
        window.localStorage.clear();
        window.sessionStorage.setItem('e2e-started', '1');
      }
    });
  });

  test('loads as the default handler demo user', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/claims$/);
    await expect(page.getByTestId('current-user')).toContainText('Alex Morgan');
    await expect(page.getByTestId('current-user')).toContainText('handler');
  });

  test('switching demo user mints a token the real API accepts, and updates the header', async ({
    page,
  }) => {
    await page.goto('/');
    await expect(page.getByTestId('current-user')).toContainText('Alex Morgan');

    const tokenResponse = page.waitForResponse(
      (r) => r.url().endsWith('/api/dev/auth/token') && r.ok(),
    );
    await page.getByTestId('role-switcher').click();
    await page.getByRole('option', { name: /Priya Shah/ }).click();
    const { accessToken, role } = (await (await tokenResponse).json()) as {
      accessToken: string;
      role: string;
    };

    expect(role).toBe('supervisor');
    await expect(page.getByTestId('current-user')).toContainText('Priya Shah');
    await expect(page.getByTestId('current-user')).toContainText('supervisor');

    const claims = await page.request.get('/api/claims', {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    expect(claims.status()).toBe(200);
  });

  test('remembers the selected demo user across a reload', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('role-switcher').click();
    await page.getByRole('option', { name: /Daniel Okafor/ }).click();
    await expect(page.getByTestId('current-user')).toContainText('Daniel Okafor');

    await page.reload();

    await expect(page.getByTestId('current-user')).toContainText('Daniel Okafor');
  });

  test('unknown routes show the not-found page', async ({ page }) => {
    await page.goto('/no-such-page');

    await expect(page.getByRole('heading', { name: 'Not found' })).toBeVisible();
  });
});
