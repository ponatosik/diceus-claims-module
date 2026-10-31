---
id: 6
title: "Exception-to-HTTP-status-code mapping"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

§10.5 specifies a structured `422` JSON shape for validation failures, but doesn't say what not-found or concurrency-conflict failures look like. Does the exception-handling middleware map every failure to `422`, or does exception type drive the status code?

## Resolution

Only validation-shaped failures get `422` — FluentValidation failures and domain business-rule violations (self-approval, invalid status transition, etc.), using the structured `errors` shape from §10.5. Everything else gets the status code that actually fits: entity-not-found → `404`, optimistic-concurrency (`RowVer`) conflict → `409`, unhandled → `500`. A single exception-handling middleware maps exception *type* to status code; `422` is not a catch-all. Full reasoning in [ADR-0006](../../docs/adr/0006-exception-to-status-code-mapping.md).
