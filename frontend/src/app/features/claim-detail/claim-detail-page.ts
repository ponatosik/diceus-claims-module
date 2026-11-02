import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { Skeleton } from '../../shared/ui/skeleton';
import { ClaimDetailStore } from './claim-detail.store';
import { CLAIM_TABS, parseClaimTab } from './claim-tabs';
import { ClaimHeader } from './claim-header';
import { ClaimOverviewTab } from './claim-overview-tab';
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
    const tab = CLAIM_TABS[index];
    void this.router.navigate([], {
      queryParams: { tab: tab === 'overview' ? null : tab, highlight: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
