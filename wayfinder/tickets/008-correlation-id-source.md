---
id: 8
title: "CorrelationId source for in-request audit entries"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

AUD-04 requires `CorrelationId` from the HTTP request context to propagate to every audit entry written within that request. Is this a bespoke generated-header convention, or does it ride on OpenTelemetry's distributed-tracing context?

## Resolution

OpenTelemetry's `Activity.Current` trace context is the source — `ClaimAuditLog.CorrelationId` is populated from the current `Activity`'s trace id, not a bespoke header. Scope is deliberately narrow: this only covers wiring enough instrumentation that the trace id is available and gets threaded into `IAuditLogService` writes during a request. Exporter/collector configuration (where traces actually go) is a deployment concern, out of scope for this map.

Note: this resolution only covers audit entries written **within** the original HTTP request. Audit entries written later by the outbox dispatcher / Hangfire jobs (see ticket "Domain event dispatch mechanism") execute outside that request context entirely — see the open ticket "CorrelationId propagation for background/outbox-dispatched audit entries" for that unresolved half.
