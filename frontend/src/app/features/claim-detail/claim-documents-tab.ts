import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { filter, switchMap } from 'rxjs';
import { ClaimDocument } from '../../api/models';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import {
  DOCUMENT_ACCEPT,
  documentTypeLabel,
  formatFileSize,
  uploadProblem,
} from './document-files';
import { DocumentsStore } from './documents.store';
import {
  UploadDocumentChoice,
  UploadDocumentDialog,
  UploadDocumentDialogData,
} from './upload-document-dialog';

/** §11.3 Tab 4 — Documents: list, upload with progress, and download through a fresh short-lived URL. */
@Component({
  selector: 'app-claim-documents-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  providers: [DocumentsStore],
  templateUrl: './claim-documents-tab.html',
  styleUrl: './claim-documents-tab.scss',
})
export class ClaimDocumentsTab {
  /** `?highlight=<claimDocumentId>` from an audit-log link. */
  readonly highlight = input<string | undefined>(undefined);

  protected readonly store = inject(DocumentsStore);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly accept = DOCUMENT_ACCEPT;
  protected readonly typeLabel = documentTypeLabel;
  protected readonly fileSize = formatFileSize;

  protected filePicked(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = ''; // so picking the same file again still fires `change`
    if (file === undefined) return;

    const problem = uploadProblem(file);
    if (problem !== null) {
      this.notifications.warning(problem);
      return;
    }

    this.dialog
      .open<UploadDocumentDialog, UploadDocumentDialogData, UploadDocumentChoice>(
        UploadDocumentDialog,
        {
          data: { file },
          width: '480px',
        },
      )
      .afterClosed()
      .pipe(
        filter((choice): choice is UploadDocumentChoice => choice !== undefined),
        switchMap((choice) => this.store.upload(file, choice.documentType, choice.notes)),
      )
      .subscribe({
        next: (document) => this.notifications.success(`${document.documentName} uploaded.`),
        error: () => undefined, // announced by the error interceptor
      });
  }

  /**
   * Opens the tab synchronously inside the click — so popup blockers allow it — then points it at a
   * download URL fetched just now (§11.3 "calls API to get SAS URL"; BR-D-02's 1-hour TTL).
   */
  protected download(document: ClaimDocument): void {
    const tab = window.open('', '_blank');
    if (tab === null) {
      this.notifications.warning('Allow pop-ups for this site to download documents.');
      return;
    }
    tab.opener = null;
    this.store.freshDownloadUrl(document.claimDocumentId).subscribe({
      next: (url) => (tab.location.href = url),
      error: (error: unknown) => {
        tab.close();
        // API failures are already announced by the error interceptor; this is "no longer on the claim".
        if (!isApiError(error) && error instanceof Error) this.notifications.warning(error.message);
      },
    });
  }
}
