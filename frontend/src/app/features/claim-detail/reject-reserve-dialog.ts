import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { startWith } from 'rxjs';

/** Asks for the rejection reason (§10.2 `{ rejectionReason }`); closes with the trimmed reason, or undefined. */
@Component({
  selector: 'app-reject-reserve-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Reject reserve</h2>
    <mat-dialog-content>
      <mat-form-field class="full">
        <mat-label>Rejection reason</mat-label>
        <textarea matInput [formControl]="reason" rows="3" required></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button type="button" [disabled]="!valid()" (click)="reject()">Reject</button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
    }
  `,
})
export class RejectReserveDialog {
  private readonly dialogRef = inject(MatDialogRef<RejectReserveDialog, string>);
  protected readonly reason = new FormControl('', { nonNullable: true });
  private readonly text = toSignal(this.reason.valueChanges.pipe(startWith('')), {
    requireSync: true,
  });
  protected readonly valid = computed(() => this.text().trim() !== '');

  protected reject(): void {
    if (this.valid()) this.dialogRef.close(this.text().trim());
  }
}
