---
id: 3
title: "Repository granularity and Unit of Work shape"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

NFR-ARCH-03 requires Unit of Work to coordinate persistence across repositories; FNOL creation needs Claim + LossEvent + Parties + RiskObjects + optional Reserve to commit atomically (§10.1). What's the repository granularity (one per aggregate vs. generic), and is there a distinct `IUnitOfWork` abstraction or does the shared `DbContext` serve as the implicit unit of work?

## Resolution

One repository per aggregate root, all sharing a single scoped `DbContext`. There **is** an explicit `IUnitOfWork` type that owns the actual `SaveChangesAsync`/transaction-commit logic; each repository's `CommitChangesAsync()` is a thin pass-through delegating to the shared `IUnitOfWork` instance. Handlers only ever depend on repositories — `IUnitOfWork` is an implementation detail repositories share, never injected into a handler directly. Full reasoning and rejected alternatives in [ADR-0003](../../docs/adr/0003-repository-unit-of-work.md).
