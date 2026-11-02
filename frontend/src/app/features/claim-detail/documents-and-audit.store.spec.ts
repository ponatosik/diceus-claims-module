import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, firstValueFrom, of } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { DocumentsApi, UploadProgress } from '../../api/documents-api';
import { ClaimDocument } from '../../api/models';
import { RUNTIME_CONFIG } from '../../core/config/runtime-config';
import { AuditStore } from './audit.store';
import { ClaimDetailStore } from './claim-detail.store';
import { DocumentsStore } from './documents.store';

const doc = (id: string, downloadUrl: string) =>
  ({ claimDocumentId: id, downloadUrl }) as ClaimDocument;

function setup(
  documentsApi: Partial<Record<keyof DocumentsApi, unknown>>,
  claimsApi: Partial<Record<keyof ClaimsApi, unknown>> = {},
) {
  const detail = {
    claimId: signal<string | null>('c-1'),
    documentsRevision: signal(0),
    auditRevision: signal(0),
    refreshAfterDocumentChange: vi.fn(),
  };
  TestBed.configureTestingModule({
    providers: [
      DocumentsStore,
      AuditStore,
      { provide: DocumentsApi, useValue: documentsApi },
      { provide: ClaimsApi, useValue: claimsApi },
      { provide: ClaimDetailStore, useValue: detail },
      { provide: RUNTIME_CONFIG, useValue: { apiBaseUrl: 'https://api.example' } },
    ],
  });
  return detail;
}

describe('DocumentsStore', () => {
  it('loads the claim’s documents, and re-loads when the documents revision moves', () => {
    const list = vi.fn(() => of([doc('d1', '/uploads/a.pdf')]));
    const detail = setup({ list });
    const store = TestBed.inject(DocumentsStore);
    TestBed.tick();
    expect(store.documents()).toHaveLength(1);

    detail.documentsRevision.set(1);
    TestBed.tick();
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('reports upload progress, then refreshes documents and the audit log once the upload completes', async () => {
    const events = new Subject<UploadProgress>();
    const detail = setup({ list: () => of([]), upload: vi.fn(() => events) });
    const store = TestBed.inject(DocumentsStore);
    const file = new File(['x'], 'a.pdf', { type: 'application/pdf' });

    const done = firstValueFrom(store.upload(file, 'PoliceReport', null));
    events.next({ percent: 40, document: null });
    expect(store.uploadPercent()).toBe(40);
    expect(detail.refreshAfterDocumentChange).not.toHaveBeenCalled();

    events.next({ percent: 100, document: doc('d2', '/uploads/b.pdf') });
    events.complete();
    await done;

    expect(detail.refreshAfterDocumentChange).toHaveBeenCalledTimes(1);
    expect(store.uploadPercent()).toBeNull();
  });

  it('fetches a fresh download URL for a document by re-listing (SAS URLs expire after an hour)', async () => {
    const list = vi
      .fn()
      .mockReturnValueOnce(of([doc('d1', '/uploads/old.pdf')]))
      .mockReturnValueOnce(of([doc('d1', '/uploads/fresh.pdf')]));
    setup({ list });
    const store = TestBed.inject(DocumentsStore);
    TestBed.tick();

    const url = await firstValueFrom(store.freshDownloadUrl('d1'));

    expect(list).toHaveBeenCalledTimes(2);
    expect(url).toBe('https://api.example/uploads/fresh.pdf');
  });
});

describe('AuditStore', () => {
  it('pages the audit log and re-loads when the audit revision moves', () => {
    const audit = vi.fn(() => of({ items: [], totalCount: 42, page: 1, pageSize: 25 }));
    const detail = setup({}, { audit });
    const store = TestBed.inject(AuditStore);
    TestBed.tick();
    expect(audit).toHaveBeenLastCalledWith('c-1', 1, 25);
    expect(store.totalCount()).toBe(42);

    store.changePage(1, 25);
    TestBed.tick();
    expect(audit).toHaveBeenLastCalledWith('c-1', 2, 25);

    detail.auditRevision.set(1);
    TestBed.tick();
    expect(audit).toHaveBeenCalledTimes(3);
  });
});
