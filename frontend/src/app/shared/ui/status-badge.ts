import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ClaimStatus, claimStatusLabel, claimStatusToken } from '../domain/claim-status';

/** §11.1's colour-coded claim status badge, shared by the dashboard and the claim detail header. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [style.--badge-colour]="colour()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      color: #fff;
      background-color: var(--badge-colour);
      white-space: nowrap;
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<ClaimStatus>();

  protected readonly label = computed(() => claimStatusLabel(this.status()));
  protected readonly colour = computed(() => `var(${claimStatusToken(this.status())})`);
}
