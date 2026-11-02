import { Page, expect, test } from '@playwright/test';
import { ApiSeeder, DEMO_USER_IDS } from './api';

/** Starts the app as a given Demo User (what the role switcher remembers). */
async function actAs(page: Page, role: keyof typeof DEMO_USER_IDS) {
  await page.addInitScript(([key, userId]) => window.localStorage.setItem(key, userId), [
    'claims.demoUserId',
    DEMO_USER_IDS[role],
  ] as const);
}

async function seedClosedClaim(request: Parameters<typeof ApiSeeder.as>[0]) {
  const api = await ApiSeeder.as(request, 'handler');
  const claim = await api.createClaim();
  await api.transition(claim.claimId, 'Open');
  await api.transition(claim.claimId, 'Closed');
  return claim;
}

test.describe('claim detail — status transitions', () => {
  test('a handler is not offered Reopen on a Closed claim', async ({ page, request }) => {
    const claim = await seedClosedClaim(request);
    await actAs(page, 'handler');

    await page.goto(`/claims/${claim.claimId}`);

    await expect(page.getByTestId('claim-number')).toHaveText(claim.claimNumber);
    await expect(page.getByTestId('claim-status')).toHaveText('Closed');
    await expect(page.getByTestId('change-status')).toHaveCount(0);
  });

  test('a supervisor reopens a Closed claim with a reason, and it shows Open', async ({
    page,
    request,
  }) => {
    const claim = await seedClosedClaim(request);
    await actAs(page, 'supervisor');

    await page.goto(`/claims/${claim.claimId}`);
    await page.getByTestId('change-status').click();
    await page.getByRole('menuitem', { name: 'Reopened' }).click();

    const dialog = page.getByRole('dialog');
    const confirm = dialog.getByRole('button', { name: 'Confirm Reopened' });
    await expect(confirm).toBeDisabled();
    await dialog.getByLabel('Reason').fill('New evidence received from the claimant.');
    await confirm.click();

    await expect(page.getByTestId('claim-status')).toHaveText('Open');
    await expect(page.getByText(`${claim.claimNumber} reopened — now Open.`)).toBeVisible();
  });

  test('closing shows the pre-flight checklist and closes a clean claim', async ({
    page,
    request,
  }) => {
    const api = await ApiSeeder.as(request, 'handler');
    const claim = await api.createClaim();
    await api.transition(claim.claimId, 'Open');
    await actAs(page, 'handler');

    await page.goto(`/claims/${claim.claimId}`);
    await page.getByTestId('change-status').click();
    await page.getByRole('menuitem', { name: 'Closed' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByTestId('check-CC-01')).toContainText('✓');
    await expect(dialog.getByTestId('check-CC-03')).toContainText('✓');
    await dialog.getByRole('button', { name: 'Confirm Closed' }).click();

    await expect(page.getByTestId('claim-status')).toHaveText('Closed');
  });

  test('an unknown claim id shows the not-found page, keeping the URL', async ({ page }) => {
    await page.goto('/claims/00000000-0000-0000-0000-00000000dead');

    await expect(page.getByRole('heading', { name: 'Not found' })).toBeVisible();
    await expect(page).toHaveURL(/\/claims\/00000000-0000-0000-0000-00000000dead$/);
  });
});
