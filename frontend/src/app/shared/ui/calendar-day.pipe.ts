import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

/**
 * A *calendar day* derived from a timestamp (loss dates), formatted as its UTC day on purpose: the
 * API turns a loss timestamp into its UTC calendar day for the list's date-range filter and for the
 * BR-C-02 in-force check (see isInForce), so showing the browser-local day could put a claim on a
 * different day than the one it filters and validates under.
 */
export function formatCalendarDay(value: string | Date | null | undefined, locale: string): string {
  return value === null || value === undefined
    ? ''
    : formatDate(value, 'mediumDate', locale, 'UTC');
}

/** The shared display for loss dates and other calendar days — see {@link formatCalendarDay}. */
@Pipe({ name: 'calendarDay' })
export class CalendarDayPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(value: string | Date | null | undefined): string {
    return formatCalendarDay(value, this.locale);
  }
}
