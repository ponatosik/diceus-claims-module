import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { startWith } from 'rxjs';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import { CalendarDayPipe } from '../../shared/ui/calendar-day.pipe';
import { PendingTracker } from '../../shared/ui/pending-tracker';
import { ClaimDetailStore } from './claim-detail.store';

const MAX_NOTES = 4000; // UpdateClaimNotesCommandValidator.MaxNotesLength

/** §11.3 Tab 1 — Overview: loss event details, severity, estimated loss, editable claim notes. */
@Component({
  selector: 'app-claim-overview-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    CalendarDayPipe,
  ],
  templateUrl: './claim-overview-tab.html',
  styleUrl: './claim-overview-tab.scss',
})
export class ClaimOverviewTab {
  private readonly store = inject(ClaimDetailStore);
  private readonly notifications = inject(NotificationService);

  protected readonly claim = this.store.claim;
  protected readonly maxNotes = MAX_NOTES;
  protected readonly saving = new PendingTracker();
  protected readonly notes = new FormControl('', {
    nonNullable: true,
    validators: Validators.maxLength(MAX_NOTES),
  });
  private readonly notesText = toSignal(this.notes.valueChanges.pipe(startWith('')), {
    requireSync: true,
  });
  protected readonly notesLength = computed(() => this.notesText().length);

  constructor() {
    // Show the server's notes — but never overwrite what the user is editing.
    effect(() => {
      const serverNotes = this.claim()?.notes ?? '';
      if (this.notes.pristine) this.notes.setValue(serverNotes);
    });
  }

  protected save(): void {
    if (this.notes.invalid) return;
    this.saving.track(this.store.updateNotes(this.notes.value)).subscribe({
      next: () => {
        this.notes.markAsPristine();
        this.notifications.success('Notes saved.');
      },
      error: (error: unknown) => {
        const messages = isApiError(error) ? (error.errors['Notes'] ?? []) : [];
        if (messages.length > 0) {
          this.notes.setErrors({ server: messages.join(' ') });
          this.notes.markAsTouched();
        }
      },
    });
  }

  protected discard(): void {
    this.notes.reset(this.claim()?.notes ?? '');
  }
}
