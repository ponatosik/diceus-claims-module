import { HttpClient, HttpEventType } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { ApiEndpoint } from './api-endpoint';
import { ClaimDocument } from './models';

/** One upload progress step; `document` is set (with percent 100) once the API has created it. */
export interface UploadProgress {
  percent: number;
  document: ClaimDocument | null;
}

/** /api/claims/{id}/documents — multipart upload with progress, and the list with download URLs (§10.1). */
@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  list(claimId: string): Observable<ClaimDocument[]> {
    return this.http.get<ClaimDocument[]>(this.documentsUrl(claimId));
  }

  upload(
    claimId: string,
    file: File,
    documentType: string,
    notes: string | null,
  ): Observable<UploadProgress> {
    // Field names match the API's UploadClaimDocumentRequest form binding.
    const form = new FormData();
    form.append('File', file, file.name);
    form.append('DocumentType', documentType);
    if (notes !== null && notes.trim() !== '') form.append('Notes', notes);

    return this.http
      .post<ClaimDocument>(this.documentsUrl(claimId), form, {
        reportProgress: true,
        observe: 'events',
      })
      .pipe(
        filter(
          (event) =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
        ),
        map((event): UploadProgress => {
          if (event.type === HttpEventType.Response) {
            return { percent: 100, document: event.body };
          }
          const total = event.total ?? 0;
          return {
            percent: total > 0 ? Math.round((event.loaded / total) * 100) : 0,
            document: null,
          };
        }),
      );
  }

  private documentsUrl(claimId: string): string {
    return this.endpoint.url(`claims/${claimId}/documents`);
  }
}
