import { Subject, throwError } from 'rxjs';
import { PendingTracker } from './pending-tracker';

describe('PendingTracker', () => {
  it('is pending while a tracked request is in flight', () => {
    const tracker = new PendingTracker();
    const response = new Subject<number>();

    tracker.track(response).subscribe();
    expect(tracker.pending()).toBe(true);

    response.next(1);
    response.complete();
    expect(tracker.pending()).toBe(false);
  });

  it('stays pending until every overlapping request has settled', () => {
    const tracker = new PendingTracker();
    const first = new Subject<void>();
    const second = new Subject<void>();

    tracker.track(first).subscribe();
    tracker.track(second).subscribe();
    first.complete();

    expect(tracker.pending()).toBe(true);
    second.complete();
    expect(tracker.pending()).toBe(false);
  });

  it('clears on error and on unsubscribe', () => {
    const tracker = new PendingTracker();

    tracker.track(throwError(() => new Error('x'))).subscribe({ error: () => undefined });
    expect(tracker.pending()).toBe(false);

    const subscription = tracker.track(new Subject<void>()).subscribe();
    subscription.unsubscribe();
    expect(tracker.pending()).toBe(false);
  });

  it('does nothing until subscribed', () => {
    const tracker = new PendingTracker();

    tracker.track(new Subject<void>());

    expect(tracker.pending()).toBe(false);
  });
});
