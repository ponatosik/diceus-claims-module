---
id: 1
title: "Claim number generation strategy"
labels: ["wayfinder:grilling"]
status: closed
assignee: agent
parent: 0
blocked_by: []
---

## Question

§5.3/BR-C-04 says claim numbers (`CLM-{YYYY}-{7-digit-seq}`) must be unique per organisation, generated atomically, with a "database SEQUENCE object or a dedicated sequence table" both acceptable. A true SQL Server `SEQUENCE` object is a single global monotonic counter — it can't natively reset per calendar year or partition per `OrganisationId` without runtime DDL, which §16.4 forbids. Which generation strategy do we use, and does the number reset each year?

## Resolution

Use a single, global SQL `SEQUENCE`, created once via EF Core migration, that **never resets**. `{YYYY}` in the rendered claim number is just the creation year — the 7-digit sequence value keeps climbing across year boundaries rather than restarting at `0000001` each January. Full reasoning and rejected alternative (per-org/year counter table) recorded in [ADR-0001](../../docs/adr/0001-claim-number-sequence.md).
