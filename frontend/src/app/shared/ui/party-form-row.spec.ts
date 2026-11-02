import { render, screen } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { createPartyForm } from '../forms/party-form';
import { PartyFormRow } from './party-form-row';

describe('PartyFormRow', () => {
  it('switches between person and company name fields with the party type', async () => {
    await render(PartyFormRow, { inputs: { form: createPartyForm() } });
    expect(screen.getByLabelText('First name')).toBeTruthy();

    await userEvent.click(screen.getByLabelText('Party type'));
    await userEvent.click(await screen.findByRole('option', { name: 'Company' }));

    expect(await screen.findByLabelText('Company name')).toBeTruthy();
    expect(screen.queryByLabelText('First name')).toBeNull();
  });

  it('emits added only for a valid party, otherwise shows what is missing', async () => {
    const form = createPartyForm();
    const added = vi.fn();
    await render(PartyFormRow, { inputs: { form }, on: { added } });

    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(added).not.toHaveBeenCalled();
    expect(screen.getByText('First and last name are required.')).toBeTruthy();

    await userEvent.type(screen.getByLabelText('First name'), 'Jane');
    await userEvent.type(screen.getByLabelText('Last name'), 'Doe');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(added).toHaveBeenCalledTimes(1);
  });
});
