// Client-side mirror of UploadClaimDocumentCommandValidator (§13) for instant feedback — the server
// still validates every upload — plus document display helpers.

export const MAX_DOCUMENT_BYTES = 50 * 1024 * 1024;

const ALLOWED_CONTENT_TYPES = new Set([
  'application/pdf',
  'image/jpeg',
  'image/png',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'text/plain',
  'text/csv',
]);

/** The file `accept` filter for the picker, matching the allowlist. */
export const DOCUMENT_ACCEPT = '.pdf,.jpg,.jpeg,.png,.docx,.xlsx,.txt,.csv';

/** Why a picked file can't be uploaded, or null when it can. */
export function uploadProblem(file: File): string | null {
  if (!ALLOWED_CONTENT_TYPES.has(file.type.toLowerCase())) {
    return `${file.name}: file type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.`;
  }
  if (file.size > MAX_DOCUMENT_BYTES) {
    return `${file.name} exceeds the 50MB size limit.`;
  }
  return null;
}

/**
 * A document's download URL is a short-lived Azure SAS URL (absolute), or — with the local-filesystem
 * fallback (BR-D-03) — a path served by the API itself, which must be resolved against the API origin.
 */
export function resolveDownloadUrl(downloadUrl: string, apiBaseUrl: string): string {
  return /^https?:\/\//i.test(downloadUrl) ? downloadUrl : `${apiBaseUrl}${downloadUrl}`;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** The Document Type options offered at upload (§9.7 examples, plus Photo). */
export const DOCUMENT_TYPES = [
  'PoliceReport',
  'MedicalReport',
  'Invoice',
  'Photo',
  'Other',
] as const;

/** `PoliceReport` → `Police report`; free-text types from elsewhere are shown the same way. */
export function documentTypeLabel(documentType: string): string {
  const spaced = documentType.replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}
