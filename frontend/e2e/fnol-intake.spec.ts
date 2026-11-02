import { expect, test } from '@playwright/test';
import { ApiSeeder } from './api';

test.describe('FNOL intake', () => {
  test('a handler logs a claim with a policy and a $5,000 initial reserve', async ({
    page,
    request,
  }) => {
    const yesterday = new Date(Date.now() - 86_400_000);
    const typedDate = `${yesterday.getMonth() + 1}/${yesterday.getDate()}/${yesterday.getFullYear()}`;

    await page.goto('/claims');
    await page.getByRole('link', { name: /Log New Claim/ }).click();
    await expect(page).toHaveURL(/\/claims\/new$/);

    // Step 1 — Policy & Loss Details
    await page.getByTestId('policy-search').fill('Meridian');
    await page.getByRole('option', { name: /POL-2024-001001/ }).click();
    await page.getByLabel('Loss date').fill(typedDate);
    await page.getByLabel('Cause of loss').fill('Fire');
    await page.getByRole('option', { name: /^Fire/ }).click();
    await page
      .getByLabel('Loss description')
      .fill('A kitchen fire spread to the loading bay overnight.');
    await expect(page.getByTestId('in-force-badge')).toHaveText(/In force on loss date/);
    await page.getByTestId('step-loss').getByRole('button', { name: 'Next' }).click();

    // Step 2 — Parties & Risk Objects
    const parties = page.getByTestId('step-parties');
    await expect(parties.getByText('At least one Claimant is required.')).toBeVisible();
    await parties.getByRole('button', { name: 'Add Party' }).click();
    await parties.getByLabel('First name').fill('Jane');
    await parties.getByLabel('Last name').fill('Doe');
    await parties.getByRole('button', { name: 'Add', exact: true }).click();
    await expect(parties.getByTestId('party-card')).toContainText('Jane Doe');
    await parties.getByRole('button', { name: 'Next' }).click();

    // Step 3 — Initial Reserve & Review
    const reserve = page.getByTestId('step-reserve');
    await reserve.getByText('Add an initial reserve').click();
    await reserve.getByLabel('Amount').fill('5000');
    await expect(reserve.getByRole('status').first()).toHaveText('✓ Auto-approved (≤ $10,000)');
    await reserve.getByRole('button', { name: 'Create Claim' }).click();

    // No risk object was added → a Warning, confirmed rather than blocking (§11.2).
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('No risk objects linked to this claim.');
    await dialog.getByRole('button', { name: 'Create anyway' }).click();

    await expect(page.getByText(/Claim CLM-\d{4}-\d{7} created\./)).toBeVisible();
    await expect(page).toHaveURL(/\/claims\/[0-9a-f-]{36}$/);

    const claimId = page.url().split('/').pop()!;
    const api = await ApiSeeder.as(request, 'handler');
    const reserves = await api.get<{ history: { approvalStatus: string; amount: number }[] }>(
      `/api/claims/${claimId}/reserves`,
    );
    expect(reserves.history).toEqual([
      expect.objectContaining({ approvalStatus: 'AutoApproved', amount: 5000 }),
    ]);
  });
});
