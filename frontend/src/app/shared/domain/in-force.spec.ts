import { isInForce } from './in-force';

const policy = { effectiveDate: '2024-01-01', expirationDate: '2026-12-31' };

describe('isInForce', () => {
  it('is in force for a loss inside the effective period', () => {
    expect(isInForce(policy, '2025-06-15T10:00:00Z')).toBe(true);
  });

  it('includes both boundary days', () => {
    expect(isInForce(policy, '2024-01-01T00:00:00Z')).toBe(true);
    expect(isInForce(policy, '2026-12-31T23:59:59Z')).toBe(true);
  });

  it('is not in force before the effective date or after expiry', () => {
    expect(isInForce(policy, '2023-12-31T23:59:59Z')).toBe(false);
    expect(isInForce(policy, '2027-01-01T00:00:00Z')).toBe(false);
  });

  it("compares the loss date's UTC calendar day, as the backend does", () => {
    // 23:30 on 31 Dec 2026 at UTC-05:00 is already 1 Jan 2027 in UTC → outside the window.
    expect(isInForce(policy, '2026-12-31T23:30:00-05:00')).toBe(false);
  });

  it('accepts a Date as well as an ISO string', () => {
    expect(isInForce(policy, new Date(Date.UTC(2025, 0, 1)))).toBe(true);
  });
});
