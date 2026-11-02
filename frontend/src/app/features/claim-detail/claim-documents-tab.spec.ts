import { signal } from '@angular/core';
import { render, screen, waitFor, within } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of } from 'rxjs';
import { ClaimDocument } from '../../api/models';
import { NotificationService } from '../../core/errors/notification.service';
import { ClaimDocumentsTab } from './claim-documents-tab';
import { DocumentsStore } from './documents.store';

const document: ClaimDocument = {
  claimDocumentId: 'd1',
  claimId: 'c-1',
  documentType: 'PoliceReport',
  documentName: 'police-report.pdf',
  contentType: 'application/pdf',
  fileSizeBytes: 2048,
  uploadedAt: '2026-11-01T10:00:00Z',
  uploadedByUserId: 'u1',
  notes: null,
  downloadUrl: '/uploads/old.pdf',
  uploadedByDisplayName: 'Alex Morgan',
};

async function renderTab() {
  const store = {
    documents: signal([document]),
    loaded: signal(true),
    uploadPercent: signal<number | null>(null),
    upload: vi.fn(() => of(document)),
    freshDownloadUrl: vi.fn(() => of('https://api.example/uploads/fresh.pdf')),
  };
  const notifications = { success: vi.fn(), warning: vi.fn(), error: vi.fn(() => of(false)) };
  const result = await render(ClaimDocumentsTab, {
    providers: [{ provide: NotificationService, useValue: notifications }],
    componentProviders: [{ provide: DocumentsStore, useValue: store }],
  });
  return { ...result, store, notifications };
}

describe('ClaimDocumentsTab (§11.3 Tab 4)', () => {
  it('lists name, type, upload date, uploader and file size', async () => {
    await renderTab();
    const row = screen.getByRole('row', { name: /police-report\.pdf/ });

    expect(within(row).getByText('Police report')).toBeTruthy();
    expect(within(row).getByText('Alex Morgan')).toBeTruthy();
    expect(within(row).getByText('2.0 KB')).toBeTruthy();
  });

  it('refuses a disallowed file before uploading anything', async () => {
    const { store, notifications } = await renderTab();

    await userEvent.upload(
      screen.getByTestId('document-file-input'),
      new File(['x'], 'a.zip', { type: 'application/zip' }),
      {
        applyAccept: false,
      },
    );

    expect(notifications.warning).toHaveBeenCalledWith(
      expect.stringContaining('file type is not allowed'),
    );
    expect(store.upload).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('asks for the document type before uploading, then uploads with it', async () => {
    const { store } = await renderTab();
    const file = new File(['%PDF'], 'invoice.pdf', { type: 'application/pdf' });

    await userEvent.upload(screen.getByTestId('document-file-input'), file);
    const dialog = await screen.findByRole('dialog');
    const uploadButton = within(dialog).getByRole('button', {
      name: 'Upload',
    }) as HTMLButtonElement;
    expect(uploadButton.disabled).toBe(true);

    await userEvent.click(within(dialog).getByLabelText(/Document type/));
    await userEvent.click(await screen.findByRole('option', { name: 'Invoice' }));
    await userEvent.click(uploadButton);

    await waitFor(() => expect(store.upload).toHaveBeenCalledWith(file, 'Invoice', null));
  });

  it('downloads through a freshly fetched URL, opened in a new tab', async () => {
    const tab = { location: { href: '' }, close: vi.fn(), opener: {} };
    const open = vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);
    const { store } = await renderTab();

    await userEvent.click(screen.getByRole('button', { name: /Download police-report\.pdf/ }));

    expect(open).toHaveBeenCalledWith('', '_blank'); // opened synchronously in the click, so it isn't popup-blocked
    expect(store.freshDownloadUrl).toHaveBeenCalledWith('d1');
    expect(tab.location.href).toBe('https://api.example/uploads/fresh.pdf');
    open.mockRestore();
  });
});
