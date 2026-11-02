import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { render, screen } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../core/errors/api-error';
import { ReasonDialog, ReasonDialogData } from './reason-dialog';

async function renderDialog(submit: ReasonDialogData['submit']) {
  const close = vi.fn();
  const result = await render(ReasonDialog, {
    providers: [
      {
        provide: MAT_DIALOG_DATA,
        useValue: {
          title: 'Reject reserve',
          label: 'Rejection reason',
          confirmLabel: 'Reject',
          submit,
        },
      },
      { provide: MatDialogRef, useValue: { close } },
    ],
  });
  return { ...result, close };
}

const confirm = () => screen.getByRole('button', { name: 'Reject' }) as HTMLButtonElement;

describe('ReasonDialog', () => {
  it('requires a reason, submits it trimmed, and closes on success', async () => {
    const submit = vi.fn<ReasonDialogData['submit']>(() => of(undefined));
    const { close } = await renderDialog(submit);
    expect(confirm().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Rejection reason/), '  Not supported by evidence ');
    await userEvent.click(confirm());

    expect(submit).toHaveBeenCalledWith('Not supported by evidence');
    expect(close).toHaveBeenCalledWith(true);
  });

  it('keeps a 422 in the dialog instead of closing (spec 011 story 11)', async () => {
    const rejected: ApiError = {
      kind: 'businessRule',
      status: 422,
      message: 'A business rule was violated.',
      errors: {},
      blockingConditions: [
        {
          code: 'BR-R-02',
          message: 'Your role does not have authority to approve this reserve amount.',
        },
      ],
    };
    const { close, fixture } = await renderDialog(() => throwError(() => rejected));

    await userEvent.type(screen.getByLabelText(/Rejection reason/), 'No');
    await userEvent.click(confirm());
    await fixture.whenStable();

    expect(
      screen.getByText('Your role does not have authority to approve this reserve amount.'),
    ).toBeTruthy();
    expect(close).not.toHaveBeenCalled();
  });
});
