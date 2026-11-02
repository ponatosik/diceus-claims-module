import {
  ASSET_TYPES,
  PARTY_ROLES,
  PARTY_TYPES,
  RESERVE_COMPONENTS,
  partyDisplayName,
  partyRoleLabel,
  reserveComponentLabel,
} from './labels';

describe('domain labels and option lists', () => {
  it('lists the backend enum values in display order', () => {
    expect(PARTY_ROLES).toEqual(['Claimant', 'Insured', 'ThirdParty', 'Witness', 'Attorney']);
    expect(PARTY_TYPES).toEqual(['Person', 'Company']);
    expect(ASSET_TYPES).toEqual(['Vehicle', 'Property', 'Person', 'Equipment', 'Other']);
    expect(RESERVE_COMPONENTS).toEqual(['Indemnity', 'Expense', 'ALAE', 'SubrogationRecoverable']);
  });

  it('labels multi-word enum values readably', () => {
    expect(partyRoleLabel('ThirdParty')).toBe('Third party');
    expect(partyRoleLabel('Claimant')).toBe('Claimant');
    expect(reserveComponentLabel('SubrogationRecoverable')).toBe('Subrogation recoverable');
    expect(reserveComponentLabel('ALAE')).toBe('ALAE');
  });

  it('names a party by the fields that match its type', () => {
    expect(
      partyDisplayName({
        partyType: 'Person',
        firstName: 'Jane',
        lastName: 'Doe',
        companyName: 'x',
      }),
    ).toBe('Jane Doe');
    expect(
      partyDisplayName({
        partyType: 'Company',
        firstName: 'x',
        lastName: 'y',
        companyName: 'Acme',
      }),
    ).toBe('Acme');
    expect(
      partyDisplayName({
        partyType: 'Person',
        firstName: null,
        lastName: 'Doe',
        companyName: null,
      }),
    ).toBe('Doe');
  });
});
