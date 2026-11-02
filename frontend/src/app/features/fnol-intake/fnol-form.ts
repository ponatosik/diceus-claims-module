import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import {
  AssetType,
  CauseOfLossCode,
  CreateClaimRequest,
  PolicySearchResult,
  ReserveComponentType,
} from '../../api/models';
import {
  PartyForm,
  PartyValue,
  blankToNull,
  createPartyForm,
  toPartyRequest,
} from '../../shared/forms/party-form';

/**
 * The FNOL intake form (§11.2): one FormGroup of three step groups. Each step group's control names
 * match the backend's property paths (camelCased), so `applyServerErrors(stepGroup, errors)` can place
 * a 422 / validate error on the exact control that caused it. Client rules exist for instant
 * feedback only; the server stays authoritative (spec 009).
 */
export function createFnolForm() {
  const form = new FormGroup({
    loss: new FormGroup(
      {
        unknownPolicy: new FormControl(false, { nonNullable: true }),
        policy: new FormControl<PolicySearchResult | null>(null),
        // Date and time are separate controls (two inputs can't share one control and stay in sync);
        // the payload combines them. The time is optional and defaults to midnight.
        lossDate: new FormControl<Date | null>(null, [Validators.required, notAfterToday]),
        lossTime: new FormControl<Date | null>(null, timeNotInFuture),
        // A searchable autocomplete: holds the typed text until an option is picked.
        causeOfLossCode: new FormControl<CauseOfLossCode | string | null>(null, [
          Validators.required,
          optionSelected,
        ]),
        lossDescription: new FormControl('', {
          nonNullable: true,
          validators: [minTrimmedLength(MIN_DESCRIPTION)],
        }),
        lossLocation: new FormControl('', { nonNullable: true }),
        estimatedLossAmount: new FormControl<number | null>(null, Validators.min(0)),
      },
      { validators: policyOrUnknown },
    ),
    parties: new FormGroup({
      parties: new FormArray<PartyForm>([], claimantRequired),
      riskObjects: new FormArray<RiskObjectForm>([]),
    }),
    reserve: new FormGroup({
      include: new FormControl(false, { nonNullable: true }),
      initialReserve: new FormGroup(
        {
          component: new FormControl<ReserveComponentType>('Indemnity', { nonNullable: true }),
          amount: new FormControl<number | null>(null, Validators.required),
          changeReason: new FormControl('Initial reserve at FNOL', {
            nonNullable: true,
            validators: [Validators.required, Validators.maxLength(500)],
          }),
        },
        { validators: reserveAmountRule },
      ),
    }),
  });

  const { lossDate, lossTime } = form.controls.loss.controls;
  lossDate.valueChanges.subscribe(() => lossTime.updateValueAndValidity({ emitEvent: false }));

  // The initial reserve is optional: its group is disabled — neither validated nor blocking — unless
  // the user chooses to add one.
  const { include, initialReserve } = form.controls.reserve.controls;
  initialReserve.disable();
  include.valueChanges.subscribe((adding) =>
    adding ? initialReserve.enable() : initialReserve.disable(),
  );

  return form;
}

export type FnolForm = ReturnType<typeof createFnolForm>;
export type FnolFormValue = ReturnType<FnolForm['getRawValue']>;

export const MIN_DESCRIPTION = 20;

// ---- parties & risk objects ------------------------------------------------------------------

export interface RiskObjectValue {
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string;
  assetReference: string;
}

export type RiskObjectForm = ReturnType<typeof createRiskObjectForm>;

/** The inline "Add Risk Object" row (§11.2 Step 2). */
export function createRiskObjectForm(value?: RiskObjectValue) {
  const form = new FormGroup({
    assetType: new FormControl<AssetType>('Property', { nonNullable: true }),
    assetDescription: new FormControl('', { nonNullable: true, validators: Validators.required }),
    damageDescription: new FormControl('', { nonNullable: true }),
    assetReference: new FormControl('', { nonNullable: true }),
  });
  if (value !== undefined) form.setValue(value);
  return form;
}

export function addParty(form: FnolForm, party: PartyValue): void {
  form.controls.parties.controls.parties.push(createPartyForm(party));
}

export function addRiskObject(form: FnolForm, riskObject: RiskObjectValue): void {
  form.controls.parties.controls.riskObjects.push(createRiskObjectForm(riskObject));
}

// ---- payload ---------------------------------------------------------------------------------

/** The review summary's data as the POST /api/claims (and /validate) body. */
export function toCreateClaimRequest(value: FnolFormValue): CreateClaimRequest {
  const { loss, parties, reserve } = value;
  return {
    policyId: loss.unknownPolicy ? null : (loss.policy?.policyId ?? null),
    lossDate: loss.lossDate === null ? '' : lossMoment(loss.lossDate, loss.lossTime).toISOString(),
    lossDescription: loss.lossDescription.trim(),
    lossLocation: blankToNull(loss.lossLocation),
    causeOfLossCode:
      typeof loss.causeOfLossCode === 'object' && loss.causeOfLossCode !== null
        ? loss.causeOfLossCode.code
        : '',
    estimatedLossAmount: loss.estimatedLossAmount,
    parties: parties.parties.map(toPartyRequest),
    // §11.2 doesn't ask which risk object is primary; the first one listed is.
    riskObjects: parties.riskObjects.map((riskObject, index) => ({
      assetType: riskObject.assetType,
      assetDescription: riskObject.assetDescription.trim(),
      damageDescription: blankToNull(riskObject.damageDescription),
      isPrimary: index === 0,
      assetReference: blankToNull(riskObject.assetReference),
    })),
    initialReserve:
      reserve.include && reserve.initialReserve.amount !== null
        ? {
            component: reserve.initialReserve.component,
            amount: reserve.initialReserve.amount,
            changeReason: reserve.initialReserve.changeReason.trim(),
          }
        : null,
  };
}

// ---- validators (mirroring CreateClaimCommandValidator / ReserveChangeRules) -----------------

/**
 * The loss moment: the picked calendar day at the picked local time. With no time, it is the picked
 * day at 00:00 UTC — the API reads a loss's day as its UTC calendar day (in-force check BR-C-02, list
 * date filter), so a date-only loss must not slip to the previous day for users east of UTC.
 */
export function lossMoment(date: Date, time: Date | null): Date {
  if (time === null) {
    return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  }
  return new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
    time.getHours(),
    time.getMinutes(),
  );
}

function notAfterToday(control: AbstractControl<Date | null>): ValidationErrors | null {
  if (control.value === null || Number.isNaN(control.value.getTime())) return null;
  const startOfTomorrow = new Date();
  startOfTomorrow.setHours(24, 0, 0, 0);
  return control.value.getTime() >= startOfTomorrow.getTime() ? { futureDate: true } : null;
}

/** Today's date with a time that hasn't happened yet is still a future loss (§8: not in the future). */
function timeNotInFuture(control: AbstractControl<Date | null>): ValidationErrors | null {
  const date = control.parent?.get('lossDate')?.value as Date | null | undefined;
  if (
    control.value === null ||
    date === null ||
    date === undefined ||
    Number.isNaN(date.getTime())
  ) {
    return null;
  }
  return lossMoment(date, control.value).getTime() > Date.now() ? { futureTime: true } : null;
}

function optionSelected(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value !== '' ? { notSelected: true } : null;
}

function minTrimmedLength(length: number): ValidatorFn {
  return (control) =>
    typeof control.value === 'string' && control.value.trim().length >= length
      ? null
      : { minDescription: { length } };
}

function policyOrUnknown(group: AbstractControl): ValidationErrors | null {
  const { unknownPolicy, policy } = group.value as {
    unknownPolicy: boolean;
    policy: PolicySearchResult | null;
  };
  return unknownPolicy || policy !== null ? null : { policyRequired: true };
}

function claimantRequired(array: AbstractControl): ValidationErrors | null {
  const parties = array.value as PartyValue[];
  return parties.some((party) => party.partyRole === 'Claimant')
    ? null
    : { claimantRequired: true };
}

/** BR-R-01 (ReserveChangeRules): positive, except SubrogationRecoverable which may be negative (never zero). */
function reserveAmountRule(group: AbstractControl): ValidationErrors | null {
  const { component, amount } = group.value as {
    component: ReserveComponentType;
    amount: number | null;
  };
  if (amount === null) return null; // `required` reports this one
  return amount !== 0 && (amount > 0 || component === 'SubrogationRecoverable')
    ? null
    : { reserveAmount: true };
}
