import { ValidationIssue } from '../../api/models';
import { FNOL_STEPS, errorsByStep, stepOfProperty } from './fnol-issues';

describe('FNOL issue → step routing (spec 009)', () => {
  it.each([
    ['PolicyId', 'loss'],
    ['LossDate', 'loss'],
    ['LossDescription', 'loss'],
    ['LossLocation', 'loss'],
    ['CauseOfLossCode', 'loss'],
    ['EstimatedLossAmount', 'loss'],
    ['Parties', 'parties'],
    ['Parties[2].Email', 'parties'],
    ['RiskObjects', 'parties'],
    ['RiskObjects[0].AssetDescription', 'parties'],
    ['InitialReserve', 'reserve'],
    ['InitialReserve.Amount', 'reserve'],
  ] as const)('%s → %s step', (property, step) => {
    expect(stepOfProperty(property)).toBe(step);
  });

  it('does not mistake a prefix for a whole property name', () => {
    expect(stepOfProperty('PartiesCount')).toBeNull();
    expect(stepOfProperty('')).toBeNull();
  });

  it('orders the steps as the wizard shows them', () => {
    expect(FNOL_STEPS).toEqual(['loss', 'parties', 'reserve']);
  });

  it('groups only Error-severity issues by step, with unknown properties under "summary"', () => {
    const issues: ValidationIssue[] = [
      {
        propertyName: 'LossDate',
        message: 'Loss date cannot be in the future.',
        severity: 'Error',
      },
      {
        propertyName: 'LossDate',
        message: 'Loss date is outside the policy effective period.',
        severity: 'Warning',
      },
      {
        propertyName: 'Parties[0].FirstName',
        message: "'First Name' must not be empty.",
        severity: 'Error',
      },
      { propertyName: 'Mystery', message: 'Something else.', severity: 'Error' },
    ];

    expect(errorsByStep(issues)).toEqual({
      loss: { LossDate: ['Loss date cannot be in the future.'] },
      parties: { 'Parties[0].FirstName': ["'First Name' must not be empty."] },
      reserve: {},
      summary: ['Something else.'],
    });
  });

  it('accepts a 422 errors dictionary as well as validate-endpoint issues', () => {
    expect(
      errorsByStep({ 'InitialReserve.Amount': ['Reserve amount must be greater than zero.'] }),
    ).toEqual({
      loss: {},
      parties: {},
      reserve: { 'InitialReserve.Amount': ['Reserve amount must be greater than zero.'] },
      summary: [],
    });
  });
});
