import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PolicyStatus } from '../../api/models';
import { PolicyPeriod, isInForce } from '../domain/in-force';

/**
 * §11.2's in-force indicator: green when an Active policy's effective period covers the loss date
 * (GLOSSARY.md "In-force"); amber when the loss date is outside the period — or, per §11.2, when the
 * policy is not Active (e.g. Expired), whatever its dates. Hidden until both are known.
 */
@Component({
  selector: 'app-in-force-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (state(); as state) {
      <span class="badge" [class.in-force]="state === 'in-force'" data-testid="in-force-badge">{{
        label()
      }}</span>
    }
  `,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font: var(--mat-sys-label-medium);
      color: #fff;
      background: var(--color-warning);
    }
    .in-force {
      background: var(--color-positive);
    }
  `,
})
export class InForceBadge {
  readonly policy = input.required<(PolicyPeriod & { status: PolicyStatus }) | null>();
  readonly lossDate = input.required<Date | null>();

  protected readonly state = computed(() => {
    const policy = this.policy();
    const lossDate = this.lossDate();
    if (policy === null || lossDate === null || Number.isNaN(lossDate.getTime())) return null;
    if (policy.status !== 'Active') return 'inactive';
    return isInForce(policy, lossDate) ? 'in-force' : 'out-of-window';
  });

  protected readonly label = computed(() => {
    switch (this.state()) {
      case 'in-force':
        return 'In force on loss date';
      case 'inactive':
        return `Policy ${this.policy()?.status}`;
      default:
        return 'Not in force on loss date';
    }
  });
}
