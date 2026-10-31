# Local Markdown Issue Tracker

This repo has no external issue tracker, so wayfinder (`.claude/skills/wayfinder`) defaults to this local, file-based convention. This document is the "tracker doc" referenced by the wayfinder skill — its **Wayfinding operations** section below is what any wayfinder session should consult for how maps/tickets/blocking/frontier are expressed here.

## Layout

```
wayfinder/
  TRACKER.md          this file
  map.md               the map (one per effort currently active; see below)
  tickets/
    NNN-slug.md         one file per ticket, NNN = zero-padded sequence, slug = kebab-case title
```

If multiple maps are ever active at once, each gets its own `map-<slug>.md` and its tickets live under `tickets/<slug>/`. With a single active map, the flat layout above is used.

## Issue = file

Every issue (map or ticket) is one markdown file with YAML frontmatter + a body. The frontmatter plays the role a real tracker's fields (id, labels, assignee, relations) would play:

```yaml
---
id: 3                        # stable, never reused
title: "Short decision title"
labels: ["wayfinder:grilling"]   # wayfinder:map | wayfinder:research | wayfinder:prototype | wayfinder:grilling | wayfinder:task
status: open                 # open | closed
assignee: null                # null = unclaimed; "agent" once a session claims it
parent: 0                     # id of the map this ticket belongs to (tickets only)
blocked_by: []                 # ids of tickets that must be closed first (native-blocking fallback)
---
```

Body for the map matches the wayfinder map-body template (Destination / Notes / Decisions so far / Not yet specified / Out of scope). Body for a ticket is `## Question` plus, once resolved, a `## Resolution` section appended (standing in for a tracker's "resolution comment").

## Wayfinding operations

- **Create the map:** write `wayfinder/map.md` with `id: 0`, `labels: ["wayfinder:map"]`.
- **Create a ticket:** write `wayfinder/tickets/NNN-slug.md`, next sequential `id`, `parent: 0`, appropriate `wayfinder:<type>` label, `status: open`, `assignee: null`.
- **Claim a ticket:** set `assignee: agent` on that ticket file. This *is* the claim — check it before starting work.
- **Block a ticket on another:** add the blocking ticket's id to this ticket's `blocked_by` list. (This tracker has no native dependency graph, so `blocked_by` is the body/frontmatter convention the wayfinder skill allows as a fallback.)
- **Query the frontier** (open, unblocked, unclaimed children of the map): a ticket is in the frontier when `status: open`, `assignee: null`, and every id in `blocked_by` points to a ticket file with `status: closed`.
- **Resolve a ticket:** append a `## Resolution` section to the ticket body with the answer, set `status: closed`. Then add one line to `map.md`'s **Decisions so far** section linking to the ticket file (relative link) with a one-line gist.
- **Research tickets / branches:** this tracker has no concept of disposable git branches for research capture; a research ticket's findings go straight into its own `## Resolution` section (and any longer artifact gets saved under `wayfinder/assets/<ticket-slug>/` and linked from the ticket).
- **Listing open tickets:** `ls wayfinder/tickets/` + grep frontmatter (`status: open`), since there's no issue-search API here.

## Why this exists

No tracker had been configured for this repo and no `/setup-matt-pocock-skills`-provided tracker doc existed at the time wayfinder was first invoked (2026-10-31). The user chose this local-markdown fallback over setting up GitHub Issues, given this is a solo take-home effort. If a real tracker (e.g. GitHub Issues) is wired up later, this directory's content should be migrated, not left to drift as a second source of truth.
