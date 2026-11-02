import { Download, expect, test } from '@playwright/test';
import { ApiSeeder } from './api';

test.describe('claim detail — documents and audit log', () => {
  test('a handler uploads a PDF, finds its audit entry, and follows the link back to it', async ({
    page,
    request,
  }) => {
    const claim = await (await ApiSeeder.as(request, 'handler')).createClaim();
    await page.goto(`/claims/${claim.claimId}?tab=documents`);

    await page.getByTestId('document-file-input').setInputFiles({
      name: 'police-report.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 e2e test document'),
    });
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Document type').click();
    await page.getByRole('option', { name: 'Police report' }).click();
    await dialog.getByRole('button', { name: 'Upload' }).click();

    const row = page.getByRole('row', { name: /police-report\.pdf/ });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Police report');
    await expect(row).toContainText('Alex Morgan');

    // Download opens a new tab on a freshly issued URL; the API serves the file as an attachment, so
    // that tab turns into a file download of the uploaded bytes.
    // Listen before clicking: the download can start before a popup listener could be attached.
    const downloadStarted = new Promise<Download>((resolve) =>
      page.context().once('page', (popup) => popup.once('download', resolve)),
    );
    await row.getByRole('button', { name: /Download police-report\.pdf/ }).click();
    const download = await downloadStarted;
    expect(download.url()).toContain('/uploads/');
    expect(download.suggestedFilename()).toMatch(/^police-report.*\.pdf$/);

    await page.getByRole('tab', { name: 'Audit Log' }).click();
    const entry = page.locator('li', { hasText: 'DOCUMENT_UPLOADED' });
    await expect(entry).toContainText('Alex Morgan');
    await entry.getByRole('link', { name: 'View document' }).click();

    await expect(page).toHaveURL(/tab=documents/);
    await expect(page.locator('tr.highlighted')).toContainText('police-report.pdf');
  });
});
