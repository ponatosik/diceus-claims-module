import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PolicyPeriod, isInForce } from '../domain/in-force';

/**
 * §11.2's In-force indicator (GLOSSARY.md "In-force"): green when the loss date falls within the
 * policy's effective period, amber when it doesn't. Hidden until both are known.
 */
@Component({
  selector: 'app-in-force-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (inForce() !== null) {
      <span class="badge" [class.in-force]="inForce()" data-testid="in-force-badge">
        {{ inForce() ? 'In force on loss date' : 'Not in force on loss date' }}
      </span>
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
  readonly policy = input.required<PolicyPeriod | null>();
  readonly lossDate = input.required<Date | null>();

  protected readonly inForce = computed(() => {
    const policy = this.policy();
    const lossDate = this.lossDate();
    return policy === null || lossDate === null || Number.isNaN(lossDate.getTime())
      ? null
      : isInForce(policy, lossDate);
  });
}
