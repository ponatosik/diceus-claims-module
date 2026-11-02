import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Shell } from './core/shell/shell';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Shell],
  template: `<app-shell />`,
})
export class App {}
