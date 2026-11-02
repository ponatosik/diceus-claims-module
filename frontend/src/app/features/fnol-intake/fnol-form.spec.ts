import { PolicySearchResult } from '../../api/models';
import { addParty, addRiskObject, createFnolForm, toCreateClaimRequest } from './fnol-form';

const policy: PolicySearchResult = {
  policyId: 'p-1',
  policyNumber: 'POL-2024-001001',
  clientName: 'Meridian Transport LLC',
  effectiveDate: '2024-01-01',
  expirationDate: '2026-12-31',
  status: 'Active',
};

function filledForm() {
  const form = createFnolForm();
  form.controls.loss.patchValue({
    policy,
    lossDate: new Date(2026, 9, 1),
    lossTime: new Date(2000, 0, 1, 9, 30),
    causeOfLossCode: { code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property', sortOrder: 1 },
    lossDescription: '  A pipe burst and flooded the basement overnight.  ',
    lossLocation: '',
    estimatedLossAmount: null,
  });
  addParty(form, {
    partyRole: 'Claimant',
    partyType: 'Person',
    firstName: 'Jane',
    lastName: 'Doe',
    companyName: 'ignored',
    email: '',
    phone: '555-0100',
  });
  return form;
}

describe('toCreateClaimRequest (review summary → API payload)', () => {
  it('maps the loss details, sending the selected policy id and the loss timestamp as ISO-8601', () => {
    const request = toCreateClaimRequest(filledForm().getRawValue());

    expect(request).toMatchObject({
      policyId: 'p-1',
      lossDate: new Date(2026, 9, 1, 9, 30).toISOString(),
      causeOfLossCode: 'COL-FIRE',
      lossDescription: 'A pipe burst and flooded the basement overnight.',
      lossLocation: null,
      estimatedLossAmount: null,
    });
  });

  it('combines the picked loss date with the optional loss time (local), defaulting to midnight', () => {
    const form = filledForm();
    form.controls.loss.patchValue({
      lossDate: new Date(2026, 9, 1),
      lossTime: new Date(2000, 0, 1, 14, 45),
    });
    expect(toCreateClaimRequest(form.getRawValue()).lossDate).toBe(
      new Date(2026, 9, 1, 14, 45).toISOString(),
    );

    // No time: the picked day itself, as a UTC calendar day — the day the API checks in-force and
    // filters against — so it can't slip to the previous day east of UTC.
    form.controls.loss.controls.lossTime.setValue(null);
    expect(toCreateClaimRequest(form.getRawValue()).lossDate).toBe('2026-10-01T00:00:00.000Z');
  });

  it('sends policyId null for Unknown Policy, even if a policy had been picked before the toggle', () => {
    const form = filledForm();
    form.controls.loss.controls.unknownPolicy.setValue(true);

    expect(toCreateClaimRequest(form.getRawValue()).policyId).toBeNull();
  });

  it('sends only the name fields that match each party type, and blanks as null', () => {
    const form = filledForm();
    addParty(form, {
      partyRole: 'ThirdParty',
      partyType: 'Company',
      firstName: 'x',
      lastName: 'y',
      companyName: 'Acme Haulage',
      email: 'claims@acme.test',
      phone: '',
    });

    expect(toCreateClaimRequest(form.getRawValue()).parties).toEqual([
      {
        partyRole: 'Claimant',
        partyType: 'Person',
        firstName: 'Jane',
        lastName: 'Doe',
        companyName: null,
        email: null,
        phone: '555-0100',
      },
      {
        partyRole: 'ThirdParty',
        partyType: 'Company',
        firstName: null,
        lastName: null,
        companyName: 'Acme Haulage',
        email: 'claims@acme.test',
        phone: null,
      },
    ]);
  });

  it('marks the first risk object primary', () => {
    const form = filledForm();
    addRiskObject(form, {
      assetType: 'Property',
      assetDescription: 'Basement',
      damageDescription: 'Water damage',
      assetReference: '',
    });
    addRiskObject(form, {
      assetType: 'Equipment',
      assetDescription: 'Boiler',
      damageDescription: '',
      assetReference: 'SN-1',
    });

    expect(toCreateClaimRequest(form.getRawValue()).riskObjects).toEqual([
      {
        assetType: 'Property',
        assetDescription: 'Basement',
        damageDescription: 'Water damage',
        isPrimary: true,
        assetReference: null,
      },
      {
        assetType: 'Equipment',
        assetDescription: 'Boiler',
        damageDescription: null,
        isPrimary: false,
        assetReference: 'SN-1',
      },
    ]);
  });

  it('omits the initial reserve unless the user chose to add one', () => {
    const form = filledForm();
    form.controls.reserve.controls.initialReserve.patchValue({
      component: 'Indemnity',
      amount: 5000,
    });

    expect(toCreateClaimRequest(form.getRawValue()).initialReserve).toBeNull();

    form.controls.reserve.controls.include.setValue(true);
    expect(toCreateClaimRequest(form.getRawValue()).initialReserve).toEqual({
      component: 'Indemnity',
      amount: 5000,
      changeReason: 'Initial reserve at FNOL',
    });
  });
});

describe('FNOL form client-side rules (responsiveness only — the server is authoritative)', () => {
  it('requires a policy or the Unknown Policy toggle', () => {
    const form = filledForm();
    form.controls.loss.controls.policy.setValue(null);
    expect(form.controls.loss.hasError('policyRequired')).toBe(true);

    form.controls.loss.controls.unknownPolicy.setValue(true);
    expect(form.controls.loss.hasError('policyRequired')).toBe(false);
  });

  it('rejects a loss date after today', () => {
    const form = filledForm();
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    form.controls.loss.controls.lossDate.setValue(tomorrow);

    expect(form.controls.loss.controls.lossDate.hasError('futureDate')).toBe(true);
  });

  it("rejects today's date with a time that hasn't happened yet", () => {
    const form = filledForm();
    const now = new Date();
    form.controls.loss.patchValue({
      lossDate: new Date(now.getFullYear(), now.getMonth(), now.getDate()),
      lossTime: new Date(now.getTime() + 3_600_000),
    });

    // Skip in the last hour of the day, when "an hour from now" is tomorrow and the time alone isn't future.
    if (new Date(now.getTime() + 3_600_000).getDate() === now.getDate()) {
      expect(form.controls.loss.controls.lossTime.hasError('futureTime')).toBe(true);
    }
  });

  it('needs at least 20 characters of description, ignoring surrounding spaces', () => {
    const form = filledForm();
    form.controls.loss.controls.lossDescription.setValue('   too short      ');

    expect(form.controls.loss.controls.lossDescription.hasError('minDescription')).toBe(true);
  });

  it('only accepts a cause of loss picked from the list, not free text', () => {
    const form = filledForm();
    form.controls.loss.controls.causeOfLossCode.setValue('Fir');

    expect(form.controls.loss.controls.causeOfLossCode.hasError('notSelected')).toBe(true);
    expect(toCreateClaimRequest(form.getRawValue()).causeOfLossCode).toBe('');
  });

  it('requires a Claimant among the parties', () => {
    const form = createFnolForm();
    expect(form.controls.parties.controls.parties.hasError('claimantRequired')).toBe(true);

    addParty(form, {
      partyRole: 'Witness',
      partyType: 'Person',
      firstName: 'A',
      lastName: 'B',
      companyName: '',
      email: '',
      phone: '',
    });
    expect(form.controls.parties.controls.parties.hasError('claimantRequired')).toBe(true);

    addParty(form, {
      partyRole: 'Claimant',
      partyType: 'Person',
      firstName: 'C',
      lastName: 'D',
      companyName: '',
      email: '',
      phone: '',
    });
    expect(form.controls.parties.controls.parties.hasError('claimantRequired')).toBe(false);
  });

  it('validates the initial reserve only when one is being added', () => {
    const form = createFnolForm();
    expect(form.controls.reserve.valid).toBe(true);

    form.controls.reserve.controls.include.setValue(true);
    expect(form.controls.reserve.valid).toBe(false); // amount missing

    form.controls.reserve.controls.initialReserve.patchValue({
      component: 'Expense',
      amount: 2500,
    });
    expect(form.controls.reserve.valid).toBe(true);
  });
});
