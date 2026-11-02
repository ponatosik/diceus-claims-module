import { Page, expect, test } from '@playwright/test';
import { ApiSeeder, DEMO_USER_IDS } from './api';

async function actAs(page: Page, role: keyof typeof DEMO_USER_IDS) {
  await page.addInitScript(([key, userId]) => window.localStorage.setItem(key, userId), [
    'claims.demoUserId',
    DEMO_USER_IDS[role],
  ] as const);
}

async function openReservePanelAndSubmit(page: Page, amount: string) {
  await page.getByRole('button', { name: 'Add Reserve' }).click();
  const panel = page.locator('app-reserve-panel');
  await panel.getByLabel('Amount').fill(amount);
  await panel.getByLabel('Reason').fill('Liability confirmed by adjuster');
  await expect(panel.getByRole('status')).toHaveText('⚠ Supervisor approval required');
  await panel.getByRole('button', { name: 'Submit reserve' }).click();
}

test.describe('claim detail — reserves', () => {
  test('a handler’s $25,000 reserve waits for a supervisor, whose approval is posted to the GL', async ({
    browser,
    request,
  }) => {
    test.setTimeout(150_000); // the outbox dispatcher runs once a minute
    const handlerApi = await ApiSeeder.as(request, 'handler');
    const claim = await handlerApi.createClaim('POL-2024-001001');

    const handlerPage = await browser.newPage();
    await actAs(handlerPage, 'handler');
    await handlerPage.goto(`/claims/${claim.claimId}?tab=reserves`);
    await openReservePanelAndSubmit(handlerPage, '25000');

    const row = handlerPage.locator('tbody tr').first();
    await expect(row).toContainText('PendingApproval');
    await expect(row.getByRole('button', { name: /^Approve/ })).toHaveCount(0);
    await expect(handlerPage.getByTestId('component-Indemnity')).toContainText(
      '$25,000.00 pending approval',
    );

    const supervisorPage = await browser.newPage();
    await actAs(supervisorPage, 'supervisor');
    await supervisorPage.goto(`/claims/${claim.claimId}?tab=reserves`);
    const pendingRow = supervisorPage.locator('tbody tr').first();
    await pendingRow.getByRole('button', { name: /^Approve/ }).click();

    await expect(pendingRow).toContainText('Approved');
    await expect(pendingRow).toContainText('Priya Shah');
    await expect(supervisorPage.getByTestId('component-Indemnity')).toContainText('$25,000.00');
    // GL posting: Pending → Posted, picked up by the 5s poll once the background job has run.
    await expect(pendingRow).toContainText('Posted', { timeout: 120_000 });
  });

  test('a supervisor cannot approve their own reserve — in the UI or through the API (BR-R-03)', async ({
    page,
    request,
  }) => {
    const claim = await (await ApiSeeder.as(request, 'handler')).createClaim('POL-2024-001001');
    await actAs(page, 'supervisor');
    await page.goto(`/claims/${claim.claimId}?tab=reserves`);

    await openReservePanelAndSubmit(page, '50000');
    const row = page.locator('tbody tr').first();
    await expect(row.getByRole('button', { name: /^Approve/ })).toBeDisabled();

    const transactionId = (await row.getAttribute('data-testid'))!.replace('txn-', '');
    const supervisorApi = await ApiSeeder.as(request, 'supervisor');
    const response = await supervisorApi.post(
      `/api/claims/${claim.claimId}/reserves/${transactionId}/approve`,
    );
    expect(response.status()).toBe(422);
    expect((await response.json()).blockingConditions[0].code).toBe('BR-R-03');
  });

  test('a reserve can be adjusted down and then reversed to zero (ADR-0011)', async ({
    page,
    request,
  }) => {
    const api = await ApiSeeder.as(request, 'handler');
    const claim = await api.createClaim('POL-2024-001001');
    await api.post(`/api/claims/${claim.claimId}/reserves`, {
      component: 'Expense',
      amount: 8000,
      changeReason: 'Expert fees',
      transactionType: 'Add',
    });
    await actAs(page, 'handler');
    await page.goto(`/claims/${claim.claimId}?tab=reserves`);
    const card = page.getByTestId('component-Expense');
    await expect(card).toContainText('$8,000.00');

    await page.getByRole('button', { name: 'Add Reserve' }).click();
    const panel = page.locator('app-reserve-panel');
    await panel.getByLabel('Component').click();
    await page.getByRole('option', { name: 'Expense' }).click();
    await panel.getByLabel('Amount').fill('-3000');
    await panel.getByLabel('Reason').fill('Expert invoice came in lower');
    await expect(panel.getByTestId('reserve-preview')).toContainText('$8,000.00 → $5,000.00');
    await panel.getByRole('button', { name: 'Submit reserve' }).click();

    await expect(card).toContainText('$5,000.00');
    await expect(page.locator('tbody tr').first()).toContainText('-$3,000.00');

    await card.getByRole('button', { name: 'Reverse' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Reverse' }).click();

    await expect(card).toContainText('$0.00');
    await expect(page.locator('tbody tr').first()).toContainText('Reverse');
  });
});
