import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { render, screen } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of, throwError } from 'rxjs';
import { TransitionPreflight } from '../../api/models';
import { ApiError } from '../../core/errors/api-error';
import { TransitionDialog, TransitionDialogData } from './transition-dialog';

const preflight = (overrides: Partial<TransitionPreflight> = {}): TransitionPreflight => ({
  isTransitionValid: true,
  blockingConditions: [],
  warnings: [],
  requiresReason: false,
  requiresJustificationNote: false,
  ...overrides,
});

async function renderDialog(
  data: Partial<TransitionDialogData> & Pick<TransitionDialogData, 'target' | 'preflight'>,
) {
  const submit = vi.fn<TransitionDialogData['submit']>(() => of(undefined));
  const close = vi.fn();
  const result = await render(TransitionDialog, {
    providers: [
      {
        provide: MAT_DIALOG_DATA,
        useValue: { claimNumber: 'CLM-2026-0000001', from: 'Open', submit, ...data },
      },
      { provide: MatDialogRef, useValue: { close } },
    ],
  });
  await result.fixture.whenStable();
  return { ...result, submit: (data.submit as typeof submit | undefined) ?? submit, close };
}

const confirmButton = () => screen.getByRole('button', { name: /Confirm/ }) as HTMLButtonElement;

describe('TransitionDialog', () => {
  describe('closing (§4.3 pre-flight checklist)', () => {
    it('lists CC-01 to CC-03, marking the failing one with its reason, and blocks confirmation', async () => {
      await renderDialog({
        target: 'Closed',
        preflight: of(
          preflight({
            blockingConditions: [
              {
                code: 'CC-01',
                message: 'No reserve components with status PendingApproval remain.',
              },
            ],
          }),
        ),
      });

      expect(screen.getByTestId('check-CC-01').textContent).toContain('✗');
      expect(screen.getByTestId('check-CC-01').textContent).toContain(
        'No reserve components with status PendingApproval remain.',
      );
      expect(screen.getByTestId('check-CC-02').textContent).toContain('✓');
      expect(screen.getByTestId('check-CC-03').textContent).toContain('✓');
      expect(confirmButton().disabled).toBe(true);
    });

    it('treats open reserves (CC-04) as a warning that requires a justification note', async () => {
      const { submit, close } = await renderDialog({
        target: 'Closed',
        preflight: of(
          preflight({
            warnings: [
              {
                code: 'CC-04',
                message:
                  'This claim has open reserves — a justification note is required to close it anyway.',
              },
            ],
            requiresJustificationNote: true,
          }),
        ),
      });

      expect(screen.getByText(/This claim has open reserves/)).toBeTruthy();
      expect(confirmButton().disabled).toBe(true);

      await userEvent.type(
        screen.getByLabelText(/Justification/),
        'Recoveries pending; reserve kept for audit.',
      );
      expect(confirmButton().disabled).toBe(false);
      await userEvent.click(confirmButton());

      expect(submit).toHaveBeenCalledWith({
        targetStatus: 'Closed',
        reason: null,
        justificationNote: 'Recoveries pending; reserve kept for audit.',
      });
      expect(close).toHaveBeenCalledWith(true);
    });
  });

  it('requires a reason when the transition does (withdrawal, reopen)', async () => {
    const { submit } = await renderDialog({
      target: 'Withdrawn',
      preflight: of(preflight({ requiresReason: true })),
    });

    expect(confirmButton().disabled).toBe(true);
    await userEvent.type(screen.getByLabelText(/Reason/), 'Claimant withdrew in writing.');
    await userEvent.click(confirmButton());

    expect(submit).toHaveBeenCalledWith({
      targetStatus: 'Withdrawn',
      reason: 'Claimant withdrew in writing.',
      justificationNote: null,
    });
  });

  it('lists blocking conditions for other transitions too, and blocks confirmation', async () => {
    await renderDialog({
      target: 'PendingPayment',
      preflight: of(
        preflight({
          blockingConditions: [
            { code: 'BR-ST-02', message: 'At least one reserve must be approved.' },
          ],
        }),
      ),
    });

    expect(screen.getByText('At least one reserve must be approved.')).toBeTruthy();
    expect(confirmButton().disabled).toBe(true);
  });

  it('shows a 422 from the transition itself inside the dialog and stays open', async () => {
    const rejected: ApiError = {
      kind: 'businessRule',
      status: 422,
      message: 'A business rule was violated.',
      errors: {},
      blockingConditions: [
        { code: 'BR-ST-04', message: 'Reopening a claim requires the Supervisor or Manager role.' },
      ],
    };
    const submit = vi.fn(() => throwError(() => rejected));
    const { close, fixture } = await renderDialog({
      target: 'UnderInvestigation',
      preflight: of(preflight()),
      submit,
    });

    await userEvent.click(confirmButton());
    await fixture.whenStable();

    expect(
      screen.getByText('Reopening a claim requires the Supervisor or Manager role.'),
    ).toBeTruthy();
    expect(close).not.toHaveBeenCalled();
  });

  it('lets the user record an optional closure reason, which is sent with the close', async () => {
    const { submit } = await renderDialog({ target: 'Closed', preflight: of(preflight()) });

    await userEvent.type(screen.getByLabelText(/Closure reason/), 'Settled in full.');
    await userEvent.click(confirmButton());

    expect(submit).toHaveBeenCalledWith({
      targetStatus: 'Closed',
      reason: 'Settled in full.',
      justificationNote: null,
    });
  });

  it('shows only the reason a transition is invalid — no misleading checklist', async () => {
    await renderDialog({
      target: 'Closed',
      preflight: of(
        preflight({
          isTransitionValid: false,
          blockingConditions: [
            { code: 'BR-ST-01', message: 'Transition from Draft to Closed is not permitted.' },
          ],
        }),
      ),
    });

    expect(screen.getByText('Transition from Draft to Closed is not permitted.')).toBeTruthy();
    expect(screen.queryByTestId('check-CC-01')).toBeNull();
    expect(confirmButton().disabled).toBe(true);
  });

  it('asks for a justification note if CC-04 only shows up when confirming', async () => {
    const openReserves: ApiError = {
      kind: 'businessRule',
      status: 422,
      message: 'A business rule was violated.',
      errors: {},
      blockingConditions: [
        {
          code: 'CC-04',
          message:
            'This claim has open reserves — provide a justification note to close it anyway.',
        },
      ],
    };
    const submit = vi.fn<TransitionDialogData['submit']>(() => throwError(() => openReserves));
    const { fixture } = await renderDialog({
      target: 'Closed',
      preflight: of(preflight()),
      submit,
    });

    await userEvent.click(confirmButton());
    await fixture.whenStable();
    expect(confirmButton().disabled).toBe(true);

    submit.mockReturnValue(of(undefined));
    await userEvent.type(screen.getByLabelText(/Justification/), 'Recoveries pending.');
    await userEvent.click(confirmButton());

    expect(submit).toHaveBeenLastCalledWith({
      targetStatus: 'Closed',
      reason: null,
      justificationNote: 'Recoveries pending.',
    });
  });

  it('shows field errors from a 422 separately from Blocking Conditions', async () => {
    const invalid: ApiError = {
      kind: 'validation',
      status: 422,
      message: 'One or more validation errors occurred.',
      errors: { Reason: ['Reason is too long.'] },
      blockingConditions: [],
    };
    const submit = vi.fn<TransitionDialogData['submit']>(() => throwError(() => invalid));
    const { fixture } = await renderDialog({
      target: 'Withdrawn',
      preflight: of(preflight({ requiresReason: true })),
      submit,
    });

    await userEvent.type(screen.getByLabelText(/^Reason/), 'x');
    await userEvent.click(confirmButton());
    await fixture.whenStable();

    expect(screen.getByText('Reason is too long.')).toBeTruthy();
    expect(screen.queryByText(/was rejected/)).toBeNull();
  });
});
