import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { latestValue } from './latest-value';

describe('latestValue', () => {
  it('follows a resource’s value, but keeps the last one while the next is loading or failed', () => {
    const hasValue = signal(false);
    const value = signal<number | undefined>(undefined);
    const resource = { hasValue, value };
    const latest = TestBed.runInInjectionContext(() => latestValue(resource));

    expect(latest()).toBeUndefined();

    hasValue.set(true);
    value.set(1);
    expect(latest()).toBe(1);

    hasValue.set(false); // re-loading
    expect(latest()).toBe(1);

    hasValue.set(true);
    value.set(2);
    expect(latest()).toBe(2);
  });
});
