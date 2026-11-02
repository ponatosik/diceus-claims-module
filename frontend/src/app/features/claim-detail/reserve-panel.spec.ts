import { signal } from '@angular/core';
import { render, screen } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of } from 'rxjs';
import { ReserveSummary } from '../../api/models';
import { NotificationService } from '../../core/errors/notification.service';
import { ReservePanel } from './reserve-panel';
import { ReservesStore } from './reserves.store';

async function renderPanel(components: ReserveSummary[]) {
  const submit = vi.fn(() => of(undefined));
  const closed = vi.fn();
  await render(ReservePanel, {
    providers: [
      { provide: ReservesStore, useValue: { components: signal(components), submit } },
      { provide: NotificationService, useValue: { success: vi.fn() } },
    ],
    on: { closed },
  });
  return { submit, closed };
}

const submitButton = () =>
  screen.getByRole('button', { name: 'Submit reserve' }) as HTMLButtonElement;

describe('ReservePanel', () => {
  it('enables Submit as the user types, and adjusts an existing component by the signed amount', async () => {
    const { submit, closed } = await renderPanel([
      {
        reserveComponentId: 'rc',
        component: 'Indemnity',
        currentAmount: 9000,
        status: 'Active',
        pendingAmount: 0,
      },
    ]);
    expect(submitButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Amount/), '-2000');
    await userEvent.type(screen.getByLabelText(/Reason/), 'Lower repair quote');

    expect(screen.getByTestId('reserve-preview').textContent).toContain('$9,000.00 → $7,000.00');
    expect(submitButton().disabled).toBe(false);
    await userEvent.click(submitButton());

    expect(submit).toHaveBeenCalledWith({
      component: 'Indemnity',
      amount: -2000,
      changeReason: 'Lower repair quote',
      transactionType: 'Adjust',
    });
    expect(closed).toHaveBeenCalled();
  });

  it('keeps Submit disabled when a decrease would take the balance below zero', async () => {
    await renderPanel([
      {
        reserveComponentId: 'rc',
        component: 'Indemnity',
        currentAmount: 1000,
        status: 'Active',
        pendingAmount: 0,
      },
    ]);

    await userEvent.type(screen.getByLabelText(/Amount/), '-1500');
    await userEvent.type(screen.getByLabelText(/Reason/), 'x');

    expect(screen.getByText('The reserve can’t go below zero.')).toBeTruthy();
    expect(submitButton().disabled).toBe(true);
  });
});
