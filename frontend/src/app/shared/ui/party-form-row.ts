import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PARTY_ROLES, PARTY_TYPES, partyRoleLabel } from '../domain/labels';
import { PartyForm } from '../forms/party-form';

/**
 * The inline "Add Party" row (§11.2 Step 2, §11.3 Tab 2): role, Person/Company, the matching name
 * fields, email and phone. Emits `added` with a valid form; the parent decides what adding means.
 */
@Component({
  selector: 'app-party-form-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <form class="draft-row" [formGroup]="form()" (ngSubmit)="submit()" aria-label="New party">
      <mat-form-field>
        <mat-label>Party role</mat-label>
        <mat-select formControlName="partyRole">
          @for (role of partyRoles; track role) {
            <mat-option [value]="role">{{ roleLabel(role) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field class="narrow">
        <mat-label>Party type</mat-label>
        <mat-select formControlName="partyType">
          @for (type of partyTypes; track type) {
            <mat-option [value]="type">{{ type }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (form().controls.partyType.value === 'Person') {
        <mat-form-field>
          <mat-label>First name</mat-label>
          <input matInput formControlName="firstName" />
          @if (form().controls.firstName.hasError('server')) {
            <mat-error>{{ form().controls.firstName.getError('server') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Last name</mat-label>
          <input matInput formControlName="lastName" />
          @if (form().controls.lastName.hasError('server')) {
            <mat-error>{{ form().controls.lastName.getError('server') }}</mat-error>
          }
        </mat-form-field>
      } @else {
        <mat-form-field class="wide">
          <mat-label>Company name</mat-label>
          <input matInput formControlName="companyName" />
          @if (form().controls.companyName.hasError('server')) {
            <mat-error>{{ form().controls.companyName.getError('server') }}</mat-error>
          }
        </mat-form-field>
      }
      <mat-form-field>
        <mat-label>Email</mat-label>
        <input matInput type="email" formControlName="email" />
        <mat-error>{{
          form().controls.email.getError('server') ?? 'Enter a valid email address.'
        }}</mat-error>
      </mat-form-field>
      <mat-form-field class="narrow">
        <mat-label>Phone</mat-label>
        <input matInput formControlName="phone" />
      </mat-form-field>
      <div class="draft-actions">
        @if (form().hasError('nameRequired') && form().touched) {
          <mat-error>{{
            form().controls.partyType.value === 'Person'
              ? 'First and last name are required.'
              : 'Company name is required.'
          }}</mat-error>
        }
        <button mat-button type="button" (click)="cancelled.emit()">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="busy()">Add</button>
      </div>
    </form>
  `,
  styles: `
    .draft-row {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 12px;
      padding: 16px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container);
    }
    .narrow {
      width: 160px;
    }
    .wide {
      flex: 1;
      min-width: 240px;
    }
    .draft-actions {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      width: 100%;
    }
  `,
})
export class PartyFormRow {
  readonly form = input.required<PartyForm>();
  /** Disables Add while the parent's request is pending (§11.4). */
  readonly busy = input(false);
  readonly added = output<void>();
  readonly cancelled = output<void>();

  protected readonly partyRoles = PARTY_ROLES;
  protected readonly partyTypes = PARTY_TYPES;
  protected readonly roleLabel = partyRoleLabel;

  protected submit(): void {
    const form = this.form();
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }
    this.added.emit();
  }
}
