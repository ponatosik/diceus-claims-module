---
id: 11
title: "CorrelationId propagation for background/outbox-dispatched audit entries"
labels: ["wayfinder:grilling"]
status: open
assignee: null
parent: 0
blocked_by: []
---

## Question

[Ticket 8 (closed)](008-correlation-id-source.md) settled that in-request audit entries get `CorrelationId` from `Activity.Current`'s trace id. But the GL posting audit entry (`GL_POSTING_SIMULATED`, written per §12.1) and the SLA monitoring job's audit entries (`SLA_BREACH_DETECTED`, §12.2) are written by Hangfire jobs executing **outside** any original HTTP request — the outbox dispatcher from [ticket 4](004-domain-events-outbox.md) runs asynchronously, decoupled from the request that triggered reserve approval.

Does the `CorrelationId` on these background-written audit entries:
- carry the **original** request's trace id (requiring it to be persisted in the `OutboxMessage` payload and threaded through to the eventual `BackgroundJob.Enqueue` call), so the full chain "claim approved → GL posted" is traceable back to one request, or
- get a **fresh** trace id tied to the background job's own execution (simpler, but breaks the traceability chain AUD-04 seems to want)?

The SLA monitoring job has no "originating request" at all (it's purely scheduled) — does it just get a fresh trace id per run, with no ambiguity there?

## Notes

This only matters for the two event types that originate from background/scheduled execution (`GL_POSTING_SIMULATED`, `GL_POSTING_FAILED`, `SLA_BREACH_DETECTED`); every other audit event type in §14.1 is written synchronously within a request and is already covered by [ticket 8](008-correlation-id-source.md).
