import { Injectable, computed, inject, signal } from '@angular/core';
import { latestValue } from '../../shared/state/latest-value';
import { rxResource } from '@angular/core/rxjs-interop';
import { ClaimsApi } from '../../api/claims-api';
import { ClaimDetailStore } from './claim-detail.store';

export const AUDIT_PAGE_SIZES = [10, 25, 50, 100] as const;

/** Audit Log tab state (§11.3 Tab 5): the reverse-chronological, append-only log, one page at a time. */
@Injectable()
export class AuditStore {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly detail = inject(ClaimDetailStore);

  /** 1-based, as the API pages. */
  readonly page = signal(1);
  readonly pageSize = signal<number>(25);

  private readonly resource = rxResource({
    params: () => {
      const claimId = this.detail.claimId();
      return claimId === null
        ? undefined
        : {
            claimId,
            page: this.page(),
            pageSize: this.pageSize(),
            revision: this.detail.auditRevision(),
          };
    },
    stream: ({ params }) => this.claimsApi.audit(params.claimId, params.page, params.pageSize),
  });

  private readonly latestPage = latestValue(this.resource);

  readonly entries = computed(() => this.latestPage()?.items ?? []);
  readonly totalCount = computed(() => this.latestPage()?.totalCount ?? 0);
  readonly loaded = computed(() => this.latestPage() !== undefined);
  readonly loading = this.resource.isLoading;

  /** `pageIndex` is the paginator's 0-based index; a page-size change returns to the first page. */
  changePage(pageIndex: number, pageSize: number): void {
    this.page.set(pageSize === this.pageSize() ? pageIndex + 1 : 1);
    this.pageSize.set(pageSize);
  }
}
