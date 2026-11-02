import { ClaimStatusReference } from '../../api/models';
import { availableTransitions } from './claim-transitions';

// The §4.2 table as GET /api/reference/claim-statuses returns it.
const REFERENCE: ClaimStatusReference[] = [
  { status: 'Draft', validNextStatuses: ['Open'] },
  {
    status: 'Open',
    validNextStatuses: ['UnderInvestigation', 'PendingPayment', 'Closed', 'Withdrawn'],
  },
  {
    status: 'UnderInvestigation',
    validNextStatuses: ['Open', 'PendingPayment', 'Closed', 'Withdrawn'],
  },
  { status: 'PendingPayment', validNextStatuses: ['Closed'] },
  { status: 'Closed', validNextStatuses: ['Reopened'] },
  { status: 'Reopened', validNextStatuses: ['Open'] },
  { status: 'Withdrawn', validNextStatuses: [] },
];

describe('availableTransitions', () => {
  it('offers the reference table’s valid next statuses for the current status', () => {
    expect(availableTransitions(REFERENCE, 'Open', 'handler')).toEqual([
      'UnderInvestigation',
      'PendingPayment',
      'Closed',
      'Withdrawn',
    ]);
    expect(availableTransitions(REFERENCE, 'Draft', 'handler')).toEqual(['Open']);
  });

  it('hides Reopen from handlers (BR-ST-04) but offers it to supervisors and managers', () => {
    expect(availableTransitions(REFERENCE, 'Closed', 'handler')).toEqual([]);
    expect(availableTransitions(REFERENCE, 'Closed', 'supervisor')).toEqual(['Reopened']);
    expect(availableTransitions(REFERENCE, 'Closed', 'manager')).toEqual(['Reopened']);
  });

  it('never offers Reopened → Open, which the API performs automatically', () => {
    expect(availableTransitions(REFERENCE, 'Reopened', 'supervisor')).toEqual([]);
  });

  it('gates nothing else by role — the API lets every role make the other transitions', () => {
    expect(availableTransitions(REFERENCE, 'UnderInvestigation', 'handler')).toEqual(
      availableTransitions(REFERENCE, 'UnderInvestigation', 'manager'),
    );
  });

  it('offers nothing from a terminal status or before the table has loaded', () => {
    expect(availableTransitions(REFERENCE, 'Withdrawn', 'manager')).toEqual([]);
    expect(availableTransitions([], 'Open', 'manager')).toEqual([]);
  });
});
