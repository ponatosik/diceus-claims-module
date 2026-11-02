import { expect, test } from '@playwright/test';
import { ApiSeeder } from './api';

test.describe('claims dashboard', () => {
  test('filters by status through the URL, and opens a claim from its row', async ({
    page,
    request,
  }) => {
    const api = await ApiSeeder.as(request, 'handler');
    const draft = await api.createClaim();
    const withdrawn = await api.createClaim();
    await api.transition(withdrawn.claimId, 'Open');
    await api.transition(withdrawn.claimId, 'Withdrawn', 'Claimant withdrew.');

    await page.goto('/claims');
    await page.getByTestId('status-filter').click();
    await page.getByRole('option', { name: 'Withdrawn' }).click();
    await page.keyboard.press('Escape');

    await expect(page).toHaveURL(/[?&]status=Withdrawn/);
    await expect(page.getByText(withdrawn.claimNumber)).toBeVisible();
    await expect(page.getByText(draft.claimNumber)).toHaveCount(0);

    // The filter lives in the URL: a reload keeps it.
    await page.reload();
    await expect(page.getByText(withdrawn.claimNumber)).toBeVisible();
    await expect(page.getByText(draft.claimNumber)).toHaveCount(0);

    await page.getByText(withdrawn.claimNumber).click();
    await expect(page).toHaveURL(new RegExp(`/claims/${withdrawn.claimId}$`));

    // Back restores the filtered list.
    await page.goBack();
    await expect(page).toHaveURL(/[?&]status=Withdrawn/);
    await expect(page.getByText(withdrawn.claimNumber)).toBeVisible();
  });

  test('searching for a claim number that matches nothing shows the empty state', async ({
    page,
  }) => {
    await page.goto('/claims?search=CLM-NO-SUCH-CLAIM');

    await expect(page.getByText('No claims match these filters')).toBeVisible();
    await page.getByRole('button', { name: 'Clear filters' }).last().click();
    await expect(page).toHaveURL(/\/claims$/);
  });
});
