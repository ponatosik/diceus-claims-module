import {
  MAX_DOCUMENT_BYTES,
  formatFileSize,
  resolveDownloadUrl,
  uploadProblem,
} from './document-files';

const file = (type: string, size = 1024, name = 'report.pdf') => {
  const f = new File(['x'], name, { type });
  Object.defineProperty(f, 'size', { value: size });
  return f;
};

describe('uploadProblem (mirrors UploadClaimDocumentCommandValidator)', () => {
  it.each(['application/pdf', 'image/jpeg', 'image/png', 'text/plain', 'text/csv'])(
    'accepts %s',
    (type) => {
      expect(uploadProblem(file(type))).toBeNull();
    },
  );

  it('accepts Word and Excel documents', () => {
    expect(
      uploadProblem(
        file('application/vnd.openxmlformats-officedocument.wordprocessingml.document'),
      ),
    ).toBeNull();
    expect(
      uploadProblem(file('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')),
    ).toBeNull();
  });

  it('rejects other types with the allowed list', () => {
    expect(uploadProblem(file('application/zip', 10, 'a.zip'))).toBe(
      'a.zip: file type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.',
    );
  });

  it('rejects files over 50MB', () => {
    expect(uploadProblem(file('application/pdf', MAX_DOCUMENT_BYTES))).toBeNull();
    expect(uploadProblem(file('application/pdf', MAX_DOCUMENT_BYTES + 1))).toBe(
      'report.pdf exceeds the 50MB size limit.',
    );
  });
});

describe('resolveDownloadUrl', () => {
  it('keeps an absolute (Azure SAS) URL as it is', () => {
    const sas = 'https://acct.blob.core.windows.net/claim-documents/a.pdf?sv=1&sig=x';
    expect(resolveDownloadUrl(sas, 'https://api.example')).toBe(sas);
  });

  it('resolves the local-storage fallback path against the API origin', () => {
    expect(resolveDownloadUrl('/uploads/org/claim/a.pdf', 'https://api.example')).toBe(
      'https://api.example/uploads/org/claim/a.pdf',
    );
    expect(resolveDownloadUrl('/uploads/org/claim/a.pdf', '')).toBe('/uploads/org/claim/a.pdf');
  });
});

describe('formatFileSize', () => {
  it.each([
    [512, '512 B'],
    [2048, '2.0 KB'],
    [5 * 1024 * 1024, '5.0 MB'],
  ])('%d → %s', (bytes, text) => {
    expect(formatFileSize(bytes)).toBe(text);
  });
});
