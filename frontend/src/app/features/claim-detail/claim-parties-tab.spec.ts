import { signal } from '@angular/core';
import { render, screen, within } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of } from 'rxjs';
import { ClaimDetail, ClaimParty } from '../../api/models';
import { NotificationService } from '../../core/errors/notification.service';
import { ClaimDetailStore } from './claim-detail.store';
import { ClaimPartiesTab } from './claim-parties-tab';

const party = (overrides: Partial<ClaimParty>): ClaimParty => ({
  claimPartyId: 'p',
  partyRole: 'Claimant',
  partyType: 'Person',
  firstName: 'Jane',
  lastName: 'Doe',
  companyName: null,
  email: 'jane@example.test',
  phone: null,
  isActive: true,
  ...overrides,
});

async function renderTab(parties: ClaimParty[]) {
  const store = {
    claim: signal({ claimId: 'c-1', parties } as unknown as ClaimDetail),
    addParty: vi.fn(() => of(undefined)),
    removeParty: vi.fn(() => of(undefined)),
  };
  await render(ClaimPartiesTab, {
    providers: [
      { provide: ClaimDetailStore, useValue: store },
      {
        provide: NotificationService,
        useValue: { success: vi.fn(), warning: vi.fn(), error: vi.fn(() => of(false)) },
      },
    ],
  });
  return store;
}

const row = (name: string) => screen.getByRole('row', { name: new RegExp(name) });
const removeButton = (name: string) =>
  within(row(name)).getByRole('button', { name: /Remove/ }) as HTMLButtonElement;

describe('ClaimPartiesTab (§11.3 Tab 2)', () => {
  it('lists every party with role, name, contact details and active status', async () => {
    await renderTab([
      party({ claimPartyId: 'a' }),
      party({
        claimPartyId: 'b',
        partyRole: 'ThirdParty',
        partyType: 'Company',
        firstName: null,
        lastName: null,
        companyName: 'Acme Haulage',
        email: null,
        phone: '555-0100',
        isActive: false,
      }),
    ]);

    expect(within(row('Jane Doe')).getByText('Claimant')).toBeTruthy();
    expect(within(row('Jane Doe')).getByText('jane@example.test')).toBeTruthy();
    expect(within(row('Jane Doe')).getByText('Active')).toBeTruthy();
    expect(within(row('Acme Haulage')).getByText('Third party')).toBeTruthy();
    expect(within(row('Acme Haulage')).getByText('555-0100')).toBeTruthy();
    expect(within(row('Acme Haulage')).getByText('Inactive')).toBeTruthy();
  });

  it('disables Remove for the last active Claimant', async () => {
    await renderTab([
      party({ claimPartyId: 'a', firstName: 'Jane' }),
      party({ claimPartyId: 'b', firstName: 'Old', isActive: false }), // an inactive claimant doesn't count
      party({ claimPartyId: 'c', partyRole: 'Witness', firstName: 'Will' }),
    ]);

    expect(removeButton('Jane Doe').disabled).toBe(true);
    expect(removeButton('Will Doe').disabled).toBe(false);
  });

  it('allows removing a Claimant while another active one remains', async () => {
    await renderTab([
      party({ claimPartyId: 'a', firstName: 'Jane' }),
      party({ claimPartyId: 'b', firstName: 'John' }),
    ]);

    expect(removeButton('Jane Doe').disabled).toBe(false);
    expect(removeButton('John Doe').disabled).toBe(false);
  });

  it('adds a party through the inline form', async () => {
    const store = await renderTab([party({ claimPartyId: 'a' })]);

    await userEvent.click(screen.getByRole('button', { name: 'Add Party' }));
    await userEvent.type(screen.getByLabelText('First name'), 'Will');
    await userEvent.type(screen.getByLabelText('Last name'), 'Witness');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(store.addParty).toHaveBeenCalledWith(
      expect.objectContaining({
        partyType: 'Person',
        firstName: 'Will',
        lastName: 'Witness',
        companyName: null,
      }),
    );
  });
});
