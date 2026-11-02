import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { Skeleton } from '../../shared/ui/skeleton';
import { ClaimDetailStore } from './claim-detail.store';
import { CLAIM_TABS, parseClaimTab, tabChangeQueryParams } from './claim-tabs';
import { ClaimHeader } from './claim-header';
import { ClaimOverviewTab } from './claim-overview-tab';
import { ClaimAuditTab } from './claim-audit-tab';
import { ClaimDocumentsTab } from './claim-documents-tab';
import { ClaimPartiesTab } from './claim-parties-tab';
import { ClaimReservesTab } from './claim-reserves-tab';

/** §11.3 Claim Detail: always-visible header over five tabbed sections. */
@Component({
  selector: 'app-claim-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatProgressBarModule,
    MatTabsModule,
    Skeleton,
    ClaimHeader,
    ClaimOverviewTab,
    ClaimPartiesTab,
    ClaimReservesTab,
    ClaimDocumentsTab,
    ClaimAuditTab,
  ],
  providers: [ClaimDetailStore],
  templateUrl: './claim-detail-page.html',
  styleUrl: './claim-detail-page.scss',
})
export class ClaimDetailPage {
  /** Route param and query params, bound by withComponentInputBinding. */
  readonly claimId = input.required<string>();
  readonly tab = input<string>();
  readonly highlight = input<string>();

  protected readonly store = inject(ClaimDetailStore);
  private readonly router = inject(Router);

  protected readonly selectedIndex = computed(() => CLAIM_TABS.indexOf(parseClaimTab(this.tab())));

  constructor() {
    effect(() => this.store.load(this.claimId()));
  }

  protected selectTab(index: number): void {
    const queryParams = tabChangeQueryParams(this.tab(), index);
    if (queryParams === null) return;
    void this.router.navigate([], { queryParams, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
