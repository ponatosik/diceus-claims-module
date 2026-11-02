import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { render, screen, waitFor, within } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { Claim, ValidationIssue } from '../../api/models';
import { PoliciesApi } from '../../api/policies-api';
import { ReferenceApi } from '../../api/reference-api';
import { NotificationService } from '../../core/errors/notification.service';
import { FnolIntakePage } from './fnol-intake-page';

@Component({ template: '' })
class StubPage {}

const createdClaim = { claimId: 'c-1', claimNumber: 'CLM-2026-0000042' } as Claim;

async function renderWizard(validateIssues: ValidationIssue[][] = [[]]) {
  const responses = [...validateIssues];
  const validate = vi.fn(() => of(responses.length > 1 ? responses.shift()! : responses[0]));
  const create = vi.fn(() => of(createdClaim));
  const success = vi.fn();
  const result = await render(FnolIntakePage, {
    providers: [
      provideRouter([{ path: 'claims/:claimId', component: StubPage }]),
      { provide: ClaimsApi, useValue: { validate, create } },
      { provide: PoliciesApi, useValue: { search: () => of([]), coverage: () => of(null) } },
      {
        provide: ReferenceApi,
        useValue: {
          causeOfLossCodes: () =>
            of([{ code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property', sortOrder: 1 }]),
        },
      },
      {
        provide: NotificationService,
        useValue: { success, warning: vi.fn(), error: vi.fn(() => of(false)) },
      },
    ],
  });
  await result.fixture.whenStable();
  return {
    ...result,
    validate,
    create,
    success,
    router: result.fixture.debugElement.injector.get(Router),
  };
}

const user = () => userEvent.setup();
const step = (name: 'loss' | 'parties' | 'reserve') => within(screen.getByTestId(`step-${name}`));

async function fillLossStep(overrides: { lossDate?: string; description?: string } = {}) {
  const u = user();
  const loss = step('loss');
  await u.click(loss.getByRole('checkbox', { name: /Unknown Policy/ }));
  await u.type(loss.getByLabelText('Loss date'), overrides.lossDate ?? '10/1/2025');
  await u.type(loss.getByLabelText('Cause of loss'), 'Fir');
  await u.click(await screen.findByRole('option', { name: /Fire/ }));
  await u.type(
    loss.getByLabelText('Loss description'),
    overrides.description ?? 'A pipe burst and flooded the basement overnight.',
  );
}

async function addClaimant() {
  const u = user();
  const parties = step('parties');
  await u.click(parties.getByRole('button', { name: 'Add Party' }));
  await u.type(parties.getByLabelText('First name'), 'Jane');
  await u.type(parties.getByLabelText('Last name'), 'Doe');
  await u.click(parties.getByRole('button', { name: 'Add' }));
}

const selectedStep = () =>
  screen.getAllByRole('tab').find((tab) => tab.getAttribute('aria-selected') === 'true')
    ?.textContent;

describe('FnolIntakePage (§11.2 multi-step FNOL form)', () => {
  it('Step 1 blocks Next on a future loss date, without calling the API', async () => {
    const { validate, fixture } = await renderWizard();
    await fillLossStep({ lossDate: '1/1/2099' });

    await user().click(step('loss').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();

    expect(step('loss').getByText('Loss date cannot be in the future.')).toBeTruthy();
    expect(validate).not.toHaveBeenCalled();
    expect(selectedStep()).toMatch(/Policy & Loss/);
  });

  it('Step 1 counts description characters and blocks Next under 20', async () => {
    const { validate, fixture } = await renderWizard();
    await fillLossStep({ description: 'Pipe burst' });

    expect(step('loss').getByText('10 / 20 minimum')).toBeTruthy();
    await user().click(step('loss').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();

    expect(validate).not.toHaveBeenCalled();
    expect(selectedStep()).toMatch(/Policy & Loss/);
  });

  it('a server error for Step 1 keeps the user on Step 1 and shows it', async () => {
    const { fixture } = await renderWizard([
      [
        {
          propertyName: 'CauseOfLossCode',
          message: 'Cause of loss code is not recognised or is inactive.',
          severity: 'Error',
        },
      ],
    ]);
    await fillLossStep();

    await user().click(step('loss').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();

    expect(
      step('loss').getAllByText('Cause of loss code is not recognised or is inactive.').length,
    ).toBeGreaterThan(0);
    expect(selectedStep()).toMatch(/Policy & Loss/);
  });

  it('Step 2 warns that a Claimant is required and blocks Next until one is added', async () => {
    const { validate, fixture } = await renderWizard();
    await fillLossStep();
    await user().click(step('loss').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();
    expect(selectedStep()).toMatch(/Parties/);

    expect(step('parties').getByText('At least one Claimant is required.')).toBeTruthy();
    await user().click(step('parties').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();

    expect(validate).toHaveBeenCalledTimes(1);
    expect(selectedStep()).toMatch(/Parties/);
  });

  it('asks for confirmation when validation returns warnings, then creates the claim and opens it', async () => {
    const warning: ValidationIssue = {
      propertyName: 'PolicyId',
      message: 'No policy linked. Policy must be associated before reserves can be set.',
      severity: 'Warning',
    };
    const { create, success, router, fixture } = await renderWizard([[], [], [warning]]);
    const navigate = vi.spyOn(router, 'navigate');
    await fillLossStep();
    await user().click(step('loss').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();
    await addClaimant();
    await user().click(step('parties').getByRole('button', { name: 'Next' }));
    await fixture.whenStable();

    await user().click(step('reserve').getByRole('button', { name: 'Create Claim' }));
    await fixture.whenStable();

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(warning.message)).toBeTruthy();
    expect(create).not.toHaveBeenCalled();

    await user().click(within(dialog).getByRole('button', { name: 'Create anyway' }));

    await waitFor(() => expect(create).toHaveBeenCalledTimes(1)); // after the dialog's close animation
    expect(success).toHaveBeenCalledWith(expect.stringContaining('CLM-2026-0000042'));
    expect(navigate).toHaveBeenCalledWith(['/claims', 'c-1']);
  });
});
