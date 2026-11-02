import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DOCUMENT_TYPES, documentTypeLabel, formatFileSize } from './document-files';

export interface UploadDocumentDialogData {
  file: File;
}

export interface UploadDocumentChoice {
  documentType: string;
  notes: string | null;
}

/** After the file picker: the Document Type (required by the API) and optional notes (spec 012 story 2). */
@Component({
  selector: 'app-upload-document-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Upload document</h2>
    <mat-dialog-content>
      <p class="file">{{ data.file.name }} · {{ size }}</p>
      <form [formGroup]="form">
        <mat-form-field class="full">
          <mat-label>Document type</mat-label>
          <mat-select formControlName="documentType" required>
            @for (type of documentTypes; track type) {
              <mat-option [value]="type">{{ typeLabel(type) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Notes (optional)</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
          <mat-error>Notes can be at most 500 characters.</mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button type="button" [disabled]="form.invalid" (click)="upload()">
        Upload
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
    }
    .file {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class UploadDocumentDialog {
  protected readonly data = inject<UploadDocumentDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UploadDocumentDialog, UploadDocumentChoice>);

  protected readonly documentTypes = DOCUMENT_TYPES;
  protected readonly typeLabel = documentTypeLabel;
  protected readonly size = formatFileSize(this.data.file.size);
  protected readonly form = new FormGroup({
    documentType: new FormControl('', { nonNullable: true, validators: Validators.required }),
    notes: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }),
  });

  protected upload(): void {
    if (this.form.invalid) return;
    const { documentType, notes } = this.form.getRawValue();
    this.dialogRef.close({ documentType, notes: notes.trim() === '' ? null : notes.trim() });
  }
}
