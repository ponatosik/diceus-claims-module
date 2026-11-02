import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { authorityTier, authorityTierLabel } from '../domain/authority-tier';

/**
 * §11.2 / §11.3's real-time authority indicator: which Authority Tier (GLOSSARY.md) the amount being
 * typed falls into — the same `|amount|` rule the API applies (ReserveAuthority.GetTier).
 */
@Component({
  selector: 'app-authority-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (tier(); as tier) {
      <span role="status" class="indicator" [class.needs-approval]="tier !== 'auto'">{{
        label()
      }}</span>
    }
  `,
  styles: `
    .indicator {
      font: var(--mat-sys-label-large);
      color: var(--color-positive);
    }
    .needs-approval {
      color: var(--color-warning);
    }
  `,
})
export class AuthorityIndicator {
  readonly amount = input.required<number | null>();

  protected readonly tier = computed(() => {
    const amount = this.amount();
    return amount === null || amount === 0 || Number.isNaN(amount) ? null : authorityTier(amount);
  });
  protected readonly label = computed(() => {
    const tier = this.tier();
    return tier === null ? '' : authorityTierLabel(tier);
  });
}
