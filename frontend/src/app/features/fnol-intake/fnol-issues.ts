import { ValidationIssue } from '../../api/models';

/** §11.2's three FNOL steps, in wizard order. */
export const FNOL_STEPS = ['loss', 'parties', 'reserve'] as const;
export type FnolStep = (typeof FNOL_STEPS)[number];

const STEP_PROPERTIES: Record<FnolStep, readonly string[]> = {
  loss: [
    'PolicyId',
    'LossDate',
    'LossDescription',
    'LossLocation',
    'CauseOfLossCode',
    'EstimatedLossAmount',
  ],
  parties: ['Parties', 'RiskObjects'],
  reserve: ['InitialReserve'],
};

/** Which step owns a server property path (`Parties[0].Email` → parties); null when none does. */
export function stepOfProperty(propertyName: string): FnolStep | null {
  const root = /^[^.[]*/.exec(propertyName)?.[0] ?? '';
  return FNOL_STEPS.find((step) => STEP_PROPERTIES[step].includes(root)) ?? null;
}

export type ErrorsByStep = Record<FnolStep, Record<string, string[]>> & { summary: string[] };

/**
 * Splits server errors across the steps that own them (spec 009 Implementation Decisions); messages
 * for properties no step owns go to the form-level summary. Accepts either the validate endpoint's
 * issue list (only Error-severity issues count — Warnings never block) or a 422's errors dictionary.
 */
export function errorsByStep(source: ValidationIssue[] | Record<string, string[]>): ErrorsByStep {
  const result: ErrorsByStep = { loss: {}, parties: {}, reserve: {}, summary: [] };
  const entries: [string, string[]][] = Array.isArray(source)
    ? source
        .filter((issue) => issue.severity === 'Error')
        .map((issue) => [issue.propertyName, [issue.message]])
    : Object.entries(source);

  for (const [property, messages] of entries) {
    const step = stepOfProperty(property);
    if (step === null) {
      result.summary.push(...messages);
    } else {
      result[step][property] = [...(result[step][property] ?? []), ...messages];
    }
  }
  return result;
}
