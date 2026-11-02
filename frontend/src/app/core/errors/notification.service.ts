import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, map } from 'rxjs';

/** §11.4's snackbar notifications, one severity per call. Errors can carry an action. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 5000, panelClass: 'snackbar-success' });
  }

  warning(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 7000, panelClass: 'snackbar-warning' });
  }

  /** Emits `true` when the user chose `action`, `false` when the snackbar was dismissed otherwise. */
  error(message: string, action = 'Dismiss'): Observable<boolean> {
    const ref = this.snackBar.open(message, action, {
      duration: 10000,
      panelClass: 'snackbar-error',
    });
    return ref
      .afterDismissed()
      .pipe(map((dismissal) => dismissal.dismissedByAction && action !== 'Dismiss'));
  }
}
