import {
  AbstractControl,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { CreatePartyRequest, PartyRole, PartyType } from '../../api/models';

/**
 * A claim party as entered in a form — shared by FNOL Step 2 and the Claim Detail Parties tab
 * (spec 010: the party row is reused). Names follow AddClaimPartyCommand/CreatePartyDto, so a 422's
 * `FirstName`-style keys land on the right controls via applyServerErrors.
 */
export interface PartyValue {
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string;
  lastName: string;
  companyName: string;
  email: string;
  phone: string;
}

export type PartyForm = ReturnType<typeof createPartyForm>;

export function createPartyForm(value?: PartyValue) {
  const form = new FormGroup(
    {
      partyRole: new FormControl<PartyRole>('Claimant', { nonNullable: true }),
      partyType: new FormControl<PartyType>('Person', { nonNullable: true }),
      firstName: new FormControl('', { nonNullable: true }),
      lastName: new FormControl('', { nonNullable: true }),
      companyName: new FormControl('', { nonNullable: true }),
      email: new FormControl('', { nonNullable: true, validators: Validators.email }),
      phone: new FormControl('', { nonNullable: true }),
    },
    { validators: partyNameRule },
  );
  if (value !== undefined) form.setValue(value);
  return form;
}

/** The API body for a party: only the name fields matching its type, blanks as null. */
export function toPartyRequest(party: PartyValue): CreatePartyRequest {
  const isPerson = party.partyType === 'Person';
  return {
    partyRole: party.partyRole,
    partyType: party.partyType,
    firstName: isPerson ? blankToNull(party.firstName) : null,
    lastName: isPerson ? blankToNull(party.lastName) : null,
    companyName: isPerson ? null : blankToNull(party.companyName),
    email: blankToNull(party.email),
    phone: blankToNull(party.phone),
  };
}

export function blankToNull(value: string): string | null {
  return value.trim() === '' ? null : value.trim();
}

/** AddClaimPartyCommandValidator: first + last name for a Person, company name for a Company. */
function partyNameRule(group: AbstractControl): ValidationErrors | null {
  const party = group.value as PartyValue;
  const missing =
    party.partyType === 'Person'
      ? party.firstName.trim() === '' || party.lastName.trim() === ''
      : party.companyName.trim() === '';
  return missing ? { nameRequired: true } : null;
}
