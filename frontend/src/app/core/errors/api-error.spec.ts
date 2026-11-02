import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

const response = (status: number, error: unknown = null) =>
  new HttpErrorResponse({ status, error, url: '/api/claims' });

describe('toApiError', () => {
  it('maps a 422 with field errors to a validation error, keeping the errors dictionary', () => {
    const apiError = toApiError(
      response(422, {
        type: 'ValidationError',
        title: 'One or more validation errors occurred.',
        status: 422,
        errors: { LossDate: ['Loss date cannot be in the future.'] },
      }),
    );

    expect(apiError.kind).toBe('validation');
    expect(apiError.status).toBe(422);
    expect(apiError.message).toBe('One or more validation errors occurred.');
    expect(apiError.errors).toEqual({ LossDate: ['Loss date cannot be in the future.'] });
    expect(apiError.blockingConditions).toEqual([]);
  });

  it('maps a 422 with blocking conditions to a business-rule error', () => {
    const apiError = toApiError(
      response(422, {
        title: 'A business rule was violated.',
        status: 422,
        blockingConditions: [{ code: 'BR-R-03', message: 'Self-approval is not permitted.' }],
      }),
    );

    expect(apiError.kind).toBe('businessRule');
    expect(apiError.blockingConditions).toEqual([
      { code: 'BR-R-03', message: 'Self-approval is not permitted.' },
    ]);
    expect(apiError.errors).toEqual({});
  });

  it.each([
    [404, 'notFound'],
    [409, 'conflict'],
    [401, 'unauthorized'],
    [403, 'unauthorized'],
    [500, 'server'],
    [503, 'server'],
    [400, 'validation'],
  ] as const)('maps HTTP %i to %s', (status, kind) => {
    expect(toApiError(response(status)).kind).toBe(kind);
  });

  it('maps status 0 (no response) to a network error', () => {
    expect(toApiError(response(0)).kind).toBe('network');
  });

  it('falls back to a readable message when the body has no title', () => {
    expect(toApiError(response(500, 'boom')).message).toBe(
      'Something went wrong on the server. Please try again.',
    );
    expect(toApiError(response(0)).message).toBe(
      'Cannot reach the server. Check your connection and try again.',
    );
  });
});
