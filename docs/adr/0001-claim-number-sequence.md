# Claim numbers use a single, never-resetting SQL SEQUENCE

Claim numbers (`CLM-{YYYY}-{7-digit-seq}`) must be generated atomically and never duplicated or reused (BR-C-04). A per-`(OrganisationId, Year)` reset — which the format's example (`CLM-2026-0000142`) implies — would require either a new `SEQUENCE` object created at runtime each year (forbidden: §16.4 bans raw DDL at runtime) or a counter table keyed by org+year. We chose the simpler option: **one global `SEQUENCE`, created once via EF Core migration, that never resets.** `{YYYY}` in the claim number is just the creation year; the 7-digit part keeps climbing across year boundaries (e.g. the first claim of 2027 could render as `CLM-2027-0000143`, continuing from 2026's last value) rather than restarting at `0000001`.

## Considered Options

- **Counter table keyed by `(OrganisationId, Year)`**, incremented via `UPDATE ... OUTPUT INSERTED` — true per-year reset, matches the example number's visual intent exactly, still migration-only DDL. Rejected for this pass to keep the write path to a single `NEXT VALUE FOR` call with no extra table/locking design; numbers not restarting each year is a cosmetic cost, not a correctness one.

## Consequences

If a true per-year reset is ever required, this is a one-way door: switching later means migrating existing claim numbers or accepting a discontinuity. Flagged here so nobody "fixes" the non-resetting behavior without knowing it was deliberate.
