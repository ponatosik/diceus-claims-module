import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { RouterLink } from '@angular/router';
import { clearServerErrors } from '../../shared/forms/apply-server-errors';
import { FNOL_STEPS, FnolStep } from './fnol-issues';
import { FnolIntakeStore } from './fnol-intake.store';
import { FnolLossStep } from './fnol-loss-step';
import { FnolPartiesStep } from './fnol-parties-step';
import { FnolReserveStep } from './fnol-reserve-step';

/**
 * §11.2 FNOL Intake Form: a linear three-step wizard. Each step validates before the user can advance —
 * client rules first, then POST /api/claims/validate for the steps filled in so far (spec 009).
 */
@Component({
  selector: 'app-fnol-intake-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatStepperModule,
    RouterLink,
    FnolLossStep,
    FnolPartiesStep,
    FnolReserveStep,
  ],
  providers: [FnolIntakeStore],
  templateUrl: './fnol-intake-page.html',
  styleUrl: './fnol-intake-page.scss',
})
export class FnolIntakePage {
  protected readonly store = inject(FnolIntakeStore);
  private readonly stepper = viewChild.required(MatStepper);

  protected readonly busy = computed(
    () => this.store.checking.pending() || this.store.creating.pending(),
  );
  /** §11.2: Create Claim is disabled until Steps 1 and 2 are valid (and while a request is pending). */
  protected readonly canCreate = computed(
    () => this.store.stepValid().loss && this.store.stepValid().parties && !this.busy(),
  );

  protected next(step: FnolStep): void {
    const group = this.store.form.controls[step];
    // Old server errors must not block: the server re-checks below and re-applies any that still hold.
    clearServerErrors(group);
    if (group.invalid) {
      group.markAllAsTouched();
      return;
    }
    this.store.checkThrough(step).subscribe({
      next: (stepWithErrors) =>
        stepWithErrors === null ? this.stepper().next() : this.goTo(stepWithErrors),
      error: () => undefined, // already announced by the error interceptor
    });
  }

  protected create(): void {
    clearServerErrors(this.store.form);
    const reserve = this.store.form.controls.reserve;
    if (reserve.enabled && reserve.invalid) {
      reserve.markAllAsTouched();
      return;
    }
    this.store.submit().subscribe({
      next: (stepWithErrors) => {
        if (stepWithErrors !== null) this.goTo(stepWithErrors);
      },
      error: () => undefined,
    });
  }

  private goTo(step: FnolStep): void {
    this.stepper().selectedIndex = FNOL_STEPS.indexOf(step);
  }
}
