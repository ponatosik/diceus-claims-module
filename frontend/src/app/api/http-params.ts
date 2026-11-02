import { HttpParams } from '@angular/common/http';

type ParamValue = string | number | readonly string[] | null | undefined;

/** Query params from an object: arrays become repeated params, blank/undefined values are omitted. */
export function toHttpParams(values: Record<string, ParamValue>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined) continue;
    if (Array.isArray(value)) {
      for (const item of value) params = params.append(key, item);
      continue;
    }
    const text = String(value).trim();
    if (text !== '') params = params.set(key, text);
  }
  return params;
}
