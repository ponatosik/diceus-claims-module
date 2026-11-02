import { HttpEventType } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ClaimDocument } from './models';
import { DocumentsApi, UploadProgress } from './documents-api';
import { apiTestProviders } from './test-providers';

describe('DocumentsApi', () => {
  let api: DocumentsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: apiTestProviders });
    api = TestBed.inject(DocumentsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uploads as multipart form data and reports progress before the created document', () => {
    const file = new File(['%PDF'], 'report.pdf', { type: 'application/pdf' });
    const events: UploadProgress[] = [];

    api
      .upload('c1', file, 'PoliceReport', 'Filed on scene')
      .subscribe((event) => events.push(event));

    const request = http.expectOne('https://api.test/api/claims/c1/documents');
    const form = request.request.body as FormData;
    expect(form.get('File')).toBeInstanceOf(File);
    expect(form.get('DocumentType')).toBe('PoliceReport');
    expect(form.get('Notes')).toBe('Filed on scene');
    expect(request.request.reportProgress).toBe(true);

    request.event({ type: HttpEventType.UploadProgress, loaded: 50, total: 200 });
    const created = { claimDocumentId: 'd1' } as ClaimDocument;
    request.flush(created);

    expect(events).toEqual([
      { percent: 25, document: null },
      { percent: 100, document: created },
    ]);
  });

  it('omits Notes when none are given, and lists documents', () => {
    api.upload('c1', new File(['x'], 'a.txt'), 'Other', null).subscribe();
    api.list('c1').subscribe();

    const uploaded = http.expectOne((r) => r.method === 'POST');
    expect((uploaded.request.body as FormData).has('Notes')).toBe(false);
    expect(http.expectOne((r) => r.method === 'GET').request.url).toBe(
      'https://api.test/api/claims/c1/documents',
    );
  });
});
