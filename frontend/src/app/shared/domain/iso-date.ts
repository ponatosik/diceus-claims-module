/**
 * Calendar dates as the API's DateOnly expects them (`YYYY-MM-DD`). Built from a Date's *local*
 * fields: a datepicker yields local midnight, which `toISOString()` would shift to the previous day
 * anywhere east of UTC.
 */
export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** `YYYY-MM-DD` → local-midnight Date, or null if it isn't a real calendar date. */
export function fromIsoDate(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (match === null) return null;
  const [year, month, day] = [Number(match[1]), Number(match[2]) - 1, Number(match[3])];
  const date = new Date(year, month, day);
  return date.getFullYear() === year && date.getMonth() === month && date.getDate() === day
    ? date
    : null;
}
