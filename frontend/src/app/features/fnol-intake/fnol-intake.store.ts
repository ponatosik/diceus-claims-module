import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import { applyServerErrors } from '../../shared/forms/apply-server-errors';
import { confirm } from '../../shared/ui/confirm-dialog';
import { PendingTracker } from '../../shared/ui/pending-tracker';
import { createFnolForm, toCreateClaimRequest } from './fnol-form';
import { ErrorsByStep, FNOL_STEPS, FnolStep, errorsByStep } from './fnol-issues';

/**
 * FNOL wizard state (§11.2): the form, the per-step server-error summaries, and the validate/create
 * round-trips. Client validators give instant feedback; the server (POST /api/claims/validate, then
 * POST /api/claims) is authoritative (spec 009).
 */
@Injectable()
export class FnolIntakeStore {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly form = createFnolForm();
  readonly checking = new PendingTracker();
  readonly creating = new PendingTracker();

  /** Re-evaluated on every form event (value, status, touched) — what the derived signals read. */
  private readonly formEvents = toSignal(this.form.events);
  readonly value = computed(() => (this.formEvents(), this.form.getRawValue()));
  readonly stepValid = computed(() => {
    this.formEvents();
    const { loss, parties, reserve } = this.form.controls;
    return { loss: loss.valid, parties: parties.valid, reserve: reserve.valid || reserve.disabled };
  });

  /** Server messages shown at the top of each step (spec 009 story 9). */
  readonly stepErrors = signal<Record<FnolStep, string[]>>({ loss: [], parties: [], reserve: [] });

  /** Validates the steps up to and including `step` on the server. Emits the earliest step with errors, or null. */
  checkThrough(step: FnolStep): Observable<FnolStep | null> {
    const steps = FNOL_STEPS.slice(0, FNOL_STEPS.indexOf(step) + 1);
    return this.checking
      .track(this.claimsApi.validate(this.request()))
      .pipe(map((issues) => this.showErrors(errorsByStep(issues), steps)));
  }

  /**
   * Final validate → confirm any Warnings → create. On success shows the new claim number and opens
   * the claim. Emits the earliest step that needs the user's attention, or null.
   */
  submit(): Observable<FnolStep | null> {
    return this.checking.track(this.claimsApi.validate(this.request())).pipe(
      switchMap((issues) => {
        const earliest = this.showErrors(errorsByStep(issues), FNOL_STEPS);
        if (earliest !== null) return of(earliest);

        const warnings = issues
          .filter((issue) => issue.severity === 'Warning')
          .map((issue) => issue.message);
        const confirmed =
          warnings.length === 0
            ? of(true)
            : confirm(this.dialog, {
                title: 'Create claim with warnings?',
                message:
                  'These issues will not block the claim, and each is recorded in its audit log:',
                items: warnings,
                confirmLabel: 'Create anyway',
                cancelLabel: 'Go back',
              });
        return confirmed.pipe(switchMap((ok) => (ok ? this.create() : of(null))));
      }),
    );
  }

  private create(): Observable<FnolStep | null> {
    return this.creating.track(this.claimsApi.create(this.request())).pipe(
      tap((claim) => {
        this.notifications.success(`Claim ${claim.claimNumber} created.`);
        void this.router.navigate(['/claims', claim.claimId]);
      }),
      map(() => null),
      catchError((error: unknown) => {
        if (!isApiError(error) || (error.kind !== 'validation' && error.kind !== 'businessRule')) {
          return throwError(() => error);
        }
        const byStep = errorsByStep(error.errors);
        byStep.summary.push(...error.blockingConditions.map((condition) => condition.message));
        return of(this.showErrors(byStep, FNOL_STEPS) ?? 'reserve');
      }),
    );
  }

  private request() {
    return toCreateClaimRequest(this.form.getRawValue());
  }

  /**
   * Places each step's server errors on its controls and in its summary, for `steps` only (on Next,
   * later steps aren't filled in yet, so their errors are irrelevant). Messages no step owns go to the
   * last step checked. Returns the earliest step with errors.
   */
  private showErrors(byStep: ErrorsByStep, steps: readonly FnolStep[]): FnolStep | null {
    const summaries = { ...this.stepErrors() };
    for (const step of steps) {
      applyServerErrors(this.form.controls[step], byStep[step]);
      summaries[step] = Object.values(byStep[step]).flat();
    }
    const last = steps[steps.length - 1];
    summaries[last] = [...summaries[last], ...byStep.summary];
    this.stepErrors.set(summaries);
    return steps.find((step) => summaries[step].length > 0) ?? null;
  }
}
