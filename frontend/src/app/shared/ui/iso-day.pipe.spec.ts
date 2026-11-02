import { formatIsoDay } from './iso-day.pipe';

describe('formatIsoDay', () => {
  it('shows a YYYY-MM-DD calendar date (DateOnly) exactly as written, in any timezone', () => {
    expect(formatIsoDay('2024-01-01', 'en-US')).toBe('Jan 1, 2024');
    expect(formatIsoDay('2026-12-31', 'en-US')).toBe('Dec 31, 2026');
  });
});
