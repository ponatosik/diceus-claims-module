import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, filter, finalize, map, tap } from 'rxjs';
import { DocumentsApi } from '../../api/documents-api';
import { ClaimDocument } from '../../api/models';
import { RUNTIME_CONFIG } from '../../core/config/runtime-config';
import { ClaimDetailStore } from './claim-detail.store';
import { resolveDownloadUrl } from './document-files';

/** Documents tab state (§11.3 Tab 4): the list, multipart upload with progress, and fresh download URLs. */
@Injectable()
export class DocumentsStore {
  private readonly documentsApi = inject(DocumentsApi);
  private readonly detail = inject(ClaimDetailStore);
  private readonly apiBaseUrl = inject(RUNTIME_CONFIG).apiBaseUrl;

  private readonly resource = rxResource({
    params: () => {
      const claimId = this.detail.claimId();
      return claimId === null ? undefined : { claimId, revision: this.detail.documentsRevision() };
    },
    stream: ({ params }) => this.documentsApi.list(params.claimId),
  });

  private readonly latestDocuments = linkedSignal<
    ClaimDocument[] | undefined,
    ClaimDocument[] | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (documents, previous) => documents ?? previous?.value,
  });

  readonly documents = computed(() => this.latestDocuments() ?? []);
  readonly loaded = computed(() => this.latestDocuments() !== undefined);
  /** 0–100 while an upload is in flight, otherwise null. */
  readonly uploadPercent = signal<number | null>(null);

  upload(file: File, documentType: string, notes: string | null): Observable<ClaimDocument> {
    const claimId = this.requireClaimId();
    this.uploadPercent.set(0);
    return this.documentsApi.upload(claimId, file, documentType, notes).pipe(
      tap((progress) => this.uploadPercent.set(progress.percent)),
      filter((progress) => progress.document !== null),
      map((progress) => progress.document!),
      tap(() => this.detail.refreshAfterDocumentChange()),
      finalize(() => this.uploadPercent.set(null)),
    );
  }

  /**
   * A just-issued download URL for one document (§11.3 "calls API to get SAS URL"): the list is
   * re-fetched rather than reusing the one on screen, whose 1-hour SAS URLs may have expired (BR-D-02).
   */
  freshDownloadUrl(documentId: string): Observable<string> {
    return this.documentsApi.list(this.requireClaimId()).pipe(
      map((documents) => {
        const document = documents.find((candidate) => candidate.claimDocumentId === documentId);
        if (document === undefined) throw new Error('The document is no longer on this claim.');
        return resolveDownloadUrl(document.downloadUrl, this.apiBaseUrl);
      }),
    );
  }

  private requireClaimId(): string {
    const claimId = this.detail.claimId();
    if (claimId === null) throw new Error('No claim loaded.');
    return claimId;
  }
}
