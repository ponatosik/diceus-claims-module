import { formatCalendarDay, formatIsoDay } from './calendar-day.pipe';

describe('formatCalendarDay', () => {
  it("shows a timestamp's UTC calendar day — the day the API filters and checks in-force against", () => {
    expect(formatCalendarDay('2026-03-01T23:30:00Z', 'en-US')).toBe('Mar 1, 2026');
    // 00:30 at UTC+02:00 is still 1 March in UTC.
    expect(formatCalendarDay('2026-03-02T00:30:00+02:00', 'en-US')).toBe('Mar 1, 2026');
  });

  it('renders nothing for a missing value', () => {
    expect(formatCalendarDay(null, 'en-US')).toBe('');
  });
});

describe('formatIsoDay', () => {
  it('shows a YYYY-MM-DD calendar date (DateOnly) exactly as written, in any timezone', () => {
    expect(formatIsoDay('2024-01-01', 'en-US')).toBe('Jan 1, 2024');
    expect(formatIsoDay('2026-12-31', 'en-US')).toBe('Dec 31, 2026');
  });
});
