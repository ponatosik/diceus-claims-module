import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** §11.4's skeleton loader: placeholder lines shown while data loads. */
@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton" role="status" aria-label="Loading">
      @for (line of lineIndexes(); track line) {
        <div class="line"></div>
      }
    </div>
  `,
  styles: `
    .line {
      height: 16px;
      margin: 10px 0;
      border-radius: 4px;
      background: linear-gradient(
        90deg,
        var(--mat-sys-surface-container-high) 25%,
        var(--mat-sys-surface-container-highest) 50%,
        var(--mat-sys-surface-container-high) 75%
      );
      background-size: 200% 100%;
      animation: shimmer 1.4s infinite;
    }
    .line:last-child {
      width: 60%;
    }
    @keyframes shimmer {
      from {
        background-position: 200% 0;
      }
      to {
        background-position: -200% 0;
      }
    }
  `,
})
export class Skeleton {
  readonly lines = input(3);

  protected readonly lineIndexes = computed(() =>
    Array.from({ length: this.lines() }, (_, i) => i),
  );
}
