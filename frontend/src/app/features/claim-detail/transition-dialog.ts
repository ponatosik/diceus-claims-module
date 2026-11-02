import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Observable, catchError, map, of, startWith } from 'rxjs';
import { BlockingCondition, TransitionPreflight, TransitionRequest } from '../../api/models';
import { isApiError } from '../../core/errors/api-error';
import { ClaimStatus, claimStatusLabel } from '../../shared/domain/claim-status';
import { PendingTracker } from '../../shared/ui/pending-tracker';

export interface TransitionDialogData {
  claimNumber: string;
  from: ClaimStatus;
  target: ClaimStatus;
  /** GET /status/preflight for the target — the Pre-flight Check (GLOSSARY.md). */
  preflight: Observable<TransitionPreflight>;
  submit: (request: TransitionRequest) => Observable<void>;
}

/** §4.3's closure conditions, shown as a checklist when closing (spec 010 story 5). CC-04 is a warning, not a check. */
const CLOSURE_CHECKS = [
  { code: 'CC-01', label: 'No reserve transactions are awaiting approval' },
  { code: 'CC-02', label: 'No unresolved critical validation issues' },
  { code: 'CC-03', label: 'At least one active Claimant' },
] as const;

/**
 * Confirmation dialog for a claim status transition (§11.3). Runs the Pre-flight Check first; confirm
 * stays disabled while anything blocks or a required reason/justification is missing. A 422 from the
 * transition itself is shown here rather than only as a snackbar (story 6). Closes with `true` on success.
 */
@Component({
  selector: 'app-transition-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './transition-dialog.html',
  styleUrl: './transition-dialog.scss',
})
export class TransitionDialog {
  protected readonly data = inject<TransitionDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TransitionDialog, boolean>);

  protected readonly targetLabel = claimStatusLabel(this.data.target);
  protected readonly fromLabel = claimStatusLabel(this.data.from);
  protected readonly isClosing = this.data.target === 'Closed';
  protected readonly submitting = new PendingTracker();

  protected readonly reason = new FormControl('', { nonNullable: true });
  protected readonly justificationNote = new FormControl('', { nonNullable: true });
  private readonly reasonText = toSignal(this.reason.valueChanges.pipe(startWith('')), {
    requireSync: true,
  });
  private readonly justificationText = toSignal(
    this.justificationNote.valueChanges.pipe(startWith('')),
    {
      requireSync: true,
    },
  );

  /** undefined while loading; null if the pre-flight request itself failed. */
  protected readonly preflight = toSignal(this.data.preflight.pipe(catchError(() => of(null))), {
    initialValue: undefined,
  });

  /** Blocking conditions from the transition attempt itself (a 422), shown in place. */
  protected readonly rejection = signal<BlockingCondition[]>([]);

  protected readonly closureChecks = computed(() => {
    const failing = this.preflight()?.blockingConditions ?? [];
    return CLOSURE_CHECKS.map((check) => ({
      ...check,
      failure: failing.find((condition) => condition.code === check.code)?.message ?? null,
    }));
  });

  /** Blocking conditions not already shown as checklist items. */
  protected readonly otherBlockingConditions = computed(() => {
    const checklistCodes: readonly string[] = this.isClosing
      ? CLOSURE_CHECKS.map((check) => check.code)
      : [];
    return (this.preflight()?.blockingConditions ?? []).filter(
      (condition) => !checklistCodes.includes(condition.code),
    );
  });

  protected readonly canConfirm = computed(() => {
    const preflight = this.preflight();
    if (preflight === undefined || preflight === null || this.submitting.pending()) return false;
    if (!preflight.isTransitionValid || preflight.blockingConditions.length > 0) return false;
    if (preflight.requiresReason && this.reasonText().trim() === '') return false;
    if (preflight.requiresJustificationNote && this.justificationText().trim() === '') return false;
    return true;
  });

  protected confirm(): void {
    if (!this.canConfirm()) return;
    const preflight = this.preflight()!;
    const request: TransitionRequest = {
      targetStatus: this.data.target,
      reason: preflight.requiresReason ? this.reasonText().trim() : null,
      justificationNote: preflight.requiresJustificationNote
        ? this.justificationText().trim()
        : null,
    };
    this.rejection.set([]);
    this.submitting.track(this.data.submit(request).pipe(map(() => true))).subscribe({
      next: () => this.dialogRef.close(true),
      error: (error: unknown) => {
        if (isApiError(error)) {
          const fieldMessages = Object.values(error.errors)
            .flat()
            .map((message) => ({ code: '', message }));
          this.rejection.set([...error.blockingConditions, ...fieldMessages]);
        }
      },
    });
  }
}
