import { HttpErrorResponse } from '@angular/common/http';

/** A named business rule currently blocking an action (GLOSSARY.md "Blocking Condition"); ADR-0008's `blockingConditions` entries. */
export interface BlockingCondition {
  code: string;
  message: string;
}

export type ApiErrorKind =
  'validation' | 'businessRule' | 'notFound' | 'conflict' | 'unauthorized' | 'server' | 'network';

/**
 * Every failed API call, normalized once at the HTTP edge (spec 007 story 7). `errors` is the
 * field-keyed §10.5 dictionary (PascalCase property paths, e.g. `Parties[0].Email`);
 * `blockingConditions` is ADR-0008's non-field rule list.
 */
export interface ApiError {
  kind: ApiErrorKind;
  status: number;
  message: string;
  errors: Record<string, string[]>;
  blockingConditions: BlockingCondition[];
}

interface ErrorResponseBody {
  title?: string;
  errors?: Record<string, string[]>;
  blockingConditions?: BlockingCondition[];
}

const fallbackMessages: Record<ApiErrorKind, string> = {
  validation: 'Some of the submitted information is invalid.',
  businessRule: 'This action is not allowed right now.',
  notFound: 'The requested record was not found.',
  conflict: 'Someone else changed this record. Reload to see the latest version.',
  unauthorized: 'You are not allowed to do that.',
  server: 'Something went wrong on the server. Please try again.',
  network: 'Cannot reach the server. Check your connection and try again.',
};

export function toApiError(response: HttpErrorResponse): ApiError {
  const body = isErrorResponseBody(response.error) ? response.error : {};
  const blockingConditions = body.blockingConditions ?? [];
  const kind = kindOf(response.status, blockingConditions.length > 0);

  return {
    kind,
    status: response.status,
    message: body.title ?? fallbackMessages[kind],
    errors: body.errors ?? {},
    blockingConditions,
  };
}

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' && value !== null && 'kind' in value && 'blockingConditions' in value
  );
}

function kindOf(status: number, hasBlockingConditions: boolean): ApiErrorKind {
  if (status === 0) return 'network';
  if (status === 422) return hasBlockingConditions ? 'businessRule' : 'validation';
  if (status === 400) return 'validation';
  if (status === 401 || status === 403) return 'unauthorized';
  if (status === 404) return 'notFound';
  if (status === 409) return 'conflict';
  return 'server';
}

function isErrorResponseBody(value: unknown): value is ErrorResponseBody {
  return typeof value === 'object' && value !== null;
}
