import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable, startWith } from 'rxjs';
import { isApiError } from '../../core/errors/api-error';
import { PendingTracker } from '../../shared/ui/pending-tracker';

export interface ReasonDialogData {
  title: string;
  message?: string;
  label: string;
  confirmLabel: string;
  submit: (reason: string) => Observable<void>;
}

/**
 * Asks for a required reason, runs the action, and stays open on a 422 to show why it was refused
 * (spec 011 story 11) — used for Reject (§10.2 `rejectionReason`) and Reverse (§8 change reason).
 * Closes with `true` on success.
 */
@Component({
  selector: 'app-reason-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      <mat-form-field class="full">
        <mat-label>{{ data.label }}</mat-label>
        <textarea matInput [formControl]="reason" rows="3" required></textarea>
      </mat-form-field>
      @if (problems().length > 0) {
        <div class="blocked" role="alert">
          @for (problem of problems(); track $index) {
            <div>{{ problem }}</div>
          }
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button type="button" [disabled]="!canConfirm()" (click)="confirm()">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
    }
    .blocked {
      padding: 12px 16px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class ReasonDialog {
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ReasonDialog, boolean>);

  protected readonly reason = new FormControl('', { nonNullable: true });
  private readonly reasonText = toSignal(this.reason.valueChanges.pipe(startWith('')), {
    requireSync: true,
  });
  protected readonly submitting = new PendingTracker();
  /** Why the API refused the action, shown in place. */
  protected readonly problems = signal<string[]>([]);
  protected readonly canConfirm = computed(
    () => this.reasonText().trim() !== '' && !this.submitting.pending(),
  );

  protected confirm(): void {
    if (!this.canConfirm()) return;
    this.problems.set([]);
    this.submitting.track(this.data.submit(this.reasonText().trim())).subscribe({
      next: () => this.dialogRef.close(true),
      error: (error: unknown) => {
        if (isApiError(error)) {
          this.problems.set([
            ...error.blockingConditions.map((condition) => condition.message),
            ...Object.values(error.errors).flat(),
          ]);
        }
      },
    });
  }
}
