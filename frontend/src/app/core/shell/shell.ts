import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MockAuthService } from '../auth/mock-auth.service';
import { PendingTracker } from '../../shared/ui/pending-tracker';

/**
 * App shell (§11.4): top bar with the logged-in Demo User's name and role, and the role switcher used
 * to exercise role-gated UI; routed content below.
 */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatToolbarModule,
    MatSelectModule,
    MatFormFieldModule,
    MatIconModule,
    RouterLink,
    RouterOutlet,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(MockAuthService);
  protected readonly switching = new PendingTracker();

  protected switchUser(userId: string): void {
    this.switching.track(this.auth.signIn(userId)).subscribe({ error: () => undefined });
  }
}
