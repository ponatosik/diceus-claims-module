---
id: 5
title: "GL posting job idempotency mechanism"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

§6.5/§12.1 require the GL posting Hangfire job to be safe to run twice with the same idempotency key (`Reserve:{ReserveId}:Change:{ChangeSequence}`) without duplicate audit entries. Application-level check-before-write, a database-level unique constraint, or both — and what happens on conflict?

## Resolution

Both, as defense in depth: the job's primary path checks `ReserveHistory.PostingStatus`/`IdempotencyKey` before writing (no-op if already `Posted`); the `IdempotencyKey` column also carries a DB-level `UNIQUE` constraint as a safety net against races the application check alone wouldn't catch. On a unique-constraint violation, the job treats it as "already processed" and **terminates without retrying as a failure** — an expected defensive outcome, not an error path, even though the primary check should make it rare. Full reasoning in [ADR-0005](../../docs/adr/0005-gl-posting-idempotency.md).
