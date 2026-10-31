# GL posting idempotency: application check-then-insert, backed by a DB unique constraint

The GL posting job must be safe to run twice with the same idempotency key (`Reserve:{ReserveId}:Change:{ChangeSequence}`, §6.5) without creating duplicate audit entries. We use defense in depth: the job's primary path checks `ReserveHistory.PostingStatus`/`IdempotencyKey` before writing (§12.1's documented behaviour — "check if IdempotencyKey already has a Posted status; if yes, no-op"), **and** the `IdempotencyKey` column carries a database-level `UNIQUE` constraint as a safety net against races the application check doesn't catch (e.g., two retries of the same job executing concurrently). If the unique constraint is violated, the job treats it as "already processed" and **terminates without retrying as a failure** — this is an expected defensive outcome, not an error path, even though the application-level check-first logic should make it rare in practice.

## Considered Options

- **Unique constraint only, no application-level pre-check**: simpler, but means every run always attempts an insert and relies on catching the constraint-violation exception as the *normal* control-flow path, which is noisier to read and to monitor than a cheap existence check that skips the write entirely in the common case. Rejected in favor of check-then-insert as primary, constraint as backstop.

## Consequences

The job's exception handler must distinguish "duplicate-key violation on the idempotency constraint" (swallow, treat as success/no-op) from every other exception (let Hangfire's retry policy handle it, eventually marking `PostingStatus = Failed` and writing `GL_POSTING_FAILED` per §12.1). Getting that distinction wrong either masks real failures or retries loop forever on a constraint violation that will never succeed.
