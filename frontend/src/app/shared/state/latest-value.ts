import { Signal, linkedSignal } from '@angular/core';

/** The parts of an Angular resource `latestValue` needs. */
export interface ValueSource<T> {
  hasValue(): boolean;
  value(): T | undefined;
}

/**
 * A resource's value that survives re-loads and failures: it keeps showing the last loaded value until
 * a new one arrives, so lists and pages don't flash empty while they refresh or poll.
 */
export function latestValue<T>(resource: ValueSource<T>): Signal<T | undefined> {
  return linkedSignal<T | undefined, T | undefined>({
    source: () => (resource.hasValue() ? resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
}
