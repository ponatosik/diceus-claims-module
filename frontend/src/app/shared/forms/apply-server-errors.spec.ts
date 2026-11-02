import { FormArray, FormControl, FormGroup, Validators } from '@angular/forms';
import { applyServerErrors, clearServerErrors } from './apply-server-errors';

function fnolForm() {
  return new FormGroup({
    lossDate: new FormControl(''),
    parties: new FormArray([
      new FormGroup({ firstName: new FormControl(''), email: new FormControl('') }),
      new FormGroup({ firstName: new FormControl(''), email: new FormControl('') }),
    ]),
    initialReserve: new FormGroup({ amount: new FormControl(0) }),
  });
}

describe('applyServerErrors', () => {
  it('sets a server error on the control matching a top-level PascalCase property', () => {
    const form = fnolForm();

    applyServerErrors(form, { LossDate: ['Loss date cannot be in the future.'] });

    expect(form.get('lossDate')?.errors).toEqual({ server: 'Loss date cannot be in the future.' });
  });

  it('follows indexed and nested paths into form arrays and groups', () => {
    const form = fnolForm();

    applyServerErrors(form, {
      'Parties[1].Email': ['Email is invalid.'],
      'InitialReserve.Amount': ['Reserve amount must be greater than zero.'],
    });

    expect(form.get(['parties', 1, 'email'])?.errors).toEqual({ server: 'Email is invalid.' });
    expect(form.get(['parties', 0, 'email'])?.errors).toBeNull();
    expect(form.get(['initialReserve', 'amount'])?.errors).toEqual({
      server: 'Reserve amount must be greater than zero.',
    });
  });

  it('marks the matched control as touched so the Material error shows immediately', () => {
    const form = fnolForm();

    applyServerErrors(form, { LossDate: ['Bad.'] });

    expect(form.get('lossDate')?.touched).toBe(true);
  });

  it('returns messages it could not place on any control, for the form-level summary', () => {
    const form = fnolForm();

    const unmatched = applyServerErrors(form, {
      Parties: ['At least one Claimant party is required to open a claim.'],
      PolicyId: ['Linked policy could not be found.'],
      LossDate: ['Bad.'],
    });

    // "Parties" resolves to the FormArray itself — still a control, so it is placed there, not returned.
    expect(form.get('parties')?.errors).toEqual({
      server: 'At least one Claimant party is required to open a claim.',
    });
    expect(unmatched).toEqual(['Linked policy could not be found.']);
  });
});

describe('clearServerErrors', () => {
  it('drops server errors everywhere in the tree but keeps client validation errors', () => {
    const form = fnolForm();
    form.get('lossDate')!.addValidators(Validators.required);
    applyServerErrors(form, {
      LossDate: ['Bad.'],
      'Parties[1].Email': ['Invalid.'],
      Parties: ['Need a claimant.'],
    });

    clearServerErrors(form);

    expect(form.get('lossDate')?.errors).toEqual({ required: true }); // client rule still applies
    expect(form.get(['parties', 1, 'email'])?.errors).toBeNull();
    expect(form.get('parties')?.errors).toBeNull();
    expect(form.valid).toBe(false);
  });
});
