import { fromIsoDate, toIsoDate } from './iso-date';

describe('ISO calendar dates (YYYY-MM-DD, backend DateOnly)', () => {
  it('formats a picker date from its local calendar fields, never shifting through UTC', () => {
    // Local midnight — what a Material datepicker yields. toISOString() would give the previous day east of UTC.
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(toIsoDate(new Date(2026, 11, 31, 23, 59))).toBe('2026-12-31');
  });

  it('parses back to a local-midnight Date', () => {
    const date = fromIsoDate('2026-03-09');

    expect(date).not.toBeNull();
    expect([date!.getFullYear(), date!.getMonth(), date!.getDate(), date!.getHours()]).toEqual([
      2026, 2, 9, 0,
    ]);
  });

  it('rejects anything that is not a real YYYY-MM-DD date', () => {
    expect(fromIsoDate('2026-02-30')).toBeNull();
    expect(fromIsoDate('09/03/2026')).toBeNull();
    expect(fromIsoDate('')).toBeNull();
  });
});
