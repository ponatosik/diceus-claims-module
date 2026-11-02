import { AssetType, PartyRole, PartyType, ReserveComponentType } from '../../api/models';

// Option lists for the enum-backed dropdowns, in display order, and the labels for enum values whose
// API spelling isn't readable copy. One place, so every screen offers and names them identically.

export const PARTY_ROLES: readonly PartyRole[] = [
  'Claimant',
  'Insured',
  'ThirdParty',
  'Witness',
  'Attorney',
];
export const PARTY_TYPES: readonly PartyType[] = ['Person', 'Company'];
export const ASSET_TYPES: readonly AssetType[] = [
  'Vehicle',
  'Property',
  'Person',
  'Equipment',
  'Other',
];
export const RESERVE_COMPONENTS: readonly ReserveComponentType[] = [
  'Indemnity',
  'Expense',
  'ALAE',
  'SubrogationRecoverable',
];

export function partyRoleLabel(role: PartyRole): string {
  return role === 'ThirdParty' ? 'Third party' : role;
}

export function reserveComponentLabel(component: ReserveComponentType): string {
  return component === 'SubrogationRecoverable' ? 'Subrogation recoverable' : component;
}

export interface PartyNameFields {
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
}

/** A person by first + last name, a company by its name. */
export function partyDisplayName(party: PartyNameFields): string {
  return party.partyType === 'Person'
    ? [party.firstName, party.lastName]
        .filter((part) => part !== null && part.trim() !== '')
        .join(' ')
    : (party.companyName ?? '');
}
