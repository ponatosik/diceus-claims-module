import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, RouterLink],
  template: `
    <section class="not-found">
      <h1>Not found</h1>
      <p>The page or claim you were looking for doesn't exist, or you don't have access to it.</p>
      <a mat-flat-button routerLink="/claims">Back to claims</a>
    </section>
  `,
  styles: `
    .not-found {
      max-width: 560px;
      margin: 96px auto;
      text-align: center;
    }
  `,
})
export class NotFoundPage {}
