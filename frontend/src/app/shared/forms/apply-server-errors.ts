import { AbstractControl, FormArray, FormGroup } from '@angular/forms';

/**
 * Places a 422's field-keyed `errors` (ApiError.errors — FluentValidation PascalCase paths such as
 * `Parties[0].Email`) onto the matching reactive-form controls as `{ server: message }`, using the
 * camelCase form-control naming convention. Returns the messages that matched no control, for the
 * top-of-step summary §11.2 asks for.
 */
export function applyServerErrors(
  form: AbstractControl,
  errors: Record<string, string[]>,
): string[] {
  const unmatched: string[] = [];

  for (const [propertyPath, messages] of Object.entries(errors)) {
    const message = messages.join(' ');
    const control = form.get(toControlPath(propertyPath));
    if (control === null) {
      unmatched.push(message);
      continue;
    }

    control.setErrors({ ...control.errors, server: message });
    control.markAsTouched();
  }

  return unmatched;
}

/** `Parties[1].Email` → `['parties', 1, 'email']`. */
function toControlPath(propertyPath: string): (string | number)[] {
  return propertyPath
    .split('.')
    .flatMap((segment) => {
      const match = /^([^[]*)\[(\d+)\]$/.exec(segment);
      return match === null ? [camelCase(segment)] : [camelCase(match[1]), Number(match[2])];
    })
    .filter((part) => part !== '');
}

function camelCase(name: string): string {
  return name.charAt(0).toLowerCase() + name.slice(1);
}

/**
 * Removes every server-set error in the tree by re-running the client validators (which replace a
 * control's errors). Call before re-checking with the server, so a stale server error — whose cause
 * the user may have fixed elsewhere — never blocks the re-check.
 */
export function clearServerErrors(control: AbstractControl): void {
  if (control instanceof FormGroup || control instanceof FormArray) {
    Object.values(control.controls).forEach((child: AbstractControl) => clearServerErrors(child));
  }
  control.updateValueAndValidity({ onlySelf: true });
}
