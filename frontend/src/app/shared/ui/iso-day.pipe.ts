import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';
import { fromIsoDate } from '../domain/iso-date';

/**
 * A `YYYY-MM-DD` calendar date (backend DateOnly — e.g. policy effective dates), shown exactly as
 * written. Parsed to local midnight and formatted locally, so no timezone can shift it a day.
 */
export function formatIsoDay(value: string | null | undefined, locale: string): string {
  const date = value === null || value === undefined ? null : fromIsoDate(value);
  return date === null ? (value ?? '') : formatDate(date, 'mediumDate', locale);
}

@Pipe({ name: 'isoDay' })
export class IsoDayPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(value: string | null | undefined): string {
    return formatIsoDay(value, this.locale);
  }
}
