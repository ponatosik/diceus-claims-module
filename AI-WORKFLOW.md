# AI Workflow Report

## AI tools used

**Claude Code**, exclusively, for the entire backend implementation — no Cursor, Copilot, or
ChatGPT involved. Two different model tiers were used deliberately for two different jobs:

- **Claude Sonnet 5** (this session's own model) did all of the actual implementation: domain
  modeling, MediatR commands/queries/handlers/validators, EF Core configurations and migrations,
  Hangfire jobs, controllers, middleware, and the full test suite (243 tests).
- **Claude Opus**, dispatched as a fresh subagent with no memory of the implementation session,
  did the **final review** at the end of every slice — deliberately a different, more capable
  model with zero context contamination from having just written the code under review.

Every commit in this repository carries a `Claude-Session` trailer pointing back to the session
that produced it — that trailer, together with `git log`, *is* this submission's AI interaction
history (§20.6); there is no separate export beyond that, since the git history already captures
the full prompt-to-commit record faithfully.

## How the workflow was structured

**Context loading.** Nothing was re-derived from memory across sessions. `REQUIREMENTS.md` (a
single, non-summarized, traceability-tagged spec compiled from the two source PDFs — `BR-*`,
`CC-*`, `FR-*`, `NFR-*` ids) was the one authoritative source for *what* to build, read section by
section as each slice needed it rather than all at once. `wayfinder/map.md` plus `docs/adr/0001`–
`0009` were the authoritative source for *how* — every architecture question `REQUIREMENTS.md`
deliberately leaves open (claim-number generation strategy, reserve balance persistence,
repository/Unit-of-Work shape, domain-event dispatch, GL-posting idempotency, exception-to-status
mapping, JWT strategy, validation pipeline shape, testing strategy) was resolved as its own
decision record *before* any implementation touched that area, specifically so implementation
prompts never had to re-litigate architecture mid-slice. `CLAUDE.md` stays loaded every session as
the standing index into all of the above.

**Delivery shape.** Work was broken into five specs (`001`–`005`, "Slice A" through "Slice E"),
each a self-contained, independently testable vertical slice (Domain & Persistence Foundation →
FNOL/Claims/Parties/Status/Reference → Reserve Management → Background Jobs → Document
Management). Each slice followed the same loop: read the slice's spec and the relevant
`REQUIREMENTS.md` sections, implement task-by-task under strict TDD (write the failing test,
watch it fail for the right reason, write the minimal code, watch it pass, commit), then dispatch
a **fresh Opus subagent with no prior context** to review the whole slice's diff against the spec
and the codebase's own standards. Every Critical/Important finding from that review got fixed
in the same session, verified RED→GREEN with a test that reproduced the finding first, with the
full suite re-run green before moving on; Minor findings were recorded, not silently fixed and not
silently dropped.

**Iteration approach beyond slices.** The same session was later asked to do two more
cross-cutting passes once all five slices were done: a Roslyn analyzer/style sweep
(`dotnet format`) across the whole backend, and a folder reorganization. Both were handled
differently from the feature slices — these aren't "write code from a spec" tasks, they're
"apply a deterministic tool's output, then use judgment about which of its suggestions actually
make sense here." See the correction examples below for what that judgment looked like in
practice.

## Representative prompts

1. **`/implement slice E`** — a slash-command invocation of this repo's `implement` skill,
   pointing at spec `005-document-management.md`. Purpose: hand off an entire vertical slice
   (storage-provider abstraction, filename sanitization, upload/list endpoints, download URLs) to
   be implemented end-to-end under TDD, reviewed by a fresh model, and fixed, without the human
   partner needing to drive each task individually.

2. **"Is backend fully implemented?"** — a deliberately open-ended status-check prompt, not an
   instruction to build anything. Purpose: force an honest audit against `REQUIREMENTS.md` §20's
   actual deliverable list, rather than a reflexive "yes" — the correct answer distinguished
   "functionally done" (all 5 specs, all §10 endpoints, 243 passing tests) from "not done at all"
   (deliverable docs, Azure deployment, CI/CD, the entire frontend), instead of overstating
   completeness.

3. **"Please go over all code style suggestions and fix them. Then organize cs files into folders
   more neatly."** — a cross-cutting quality pass with no spec to follow. Purpose: surface
   everything `dotnet format analyzers`/`style` flags at `info` severity (nothing shows at the
   default `dotnet build` severity, since this repo has no `.editorconfig`), then apply judgment
   about which suggestions are genuine improvements versus which would fight the codebase's own
   conventions — see the first correction example below for what that judgment call looked like.

## What was AI-generated vs. manually designed/refined

**AI-generated**, in the direct sense of "Claude wrote the code": essentially all of it — every
entity, handler, validator, EF configuration, migration, controller, and test in this repository.
That is expected and explicitly not penalized per §21.3.

**Human-directed**, in the sense that actually matters for evaluating judgment: every consequential
architectural call was either (a) decided *before* implementation, recorded as a wayfinder ticket
resolution or an ADR with its considered-and-rejected alternatives written down, so the
implementation prompts had no architecture decisions left to improvise; or (b) escalated as an
explicit question when the tradeoff genuinely had no obviously-correct default — for example, when
asked to "organize cs files into folders more neatly," the honest options were a full reorg that
matches this codebase's existing folder-equals-namespace convention (touching ~90 files'
`using` statements, compiler-checked but a wide diff) versus a lighter touch that breaks that
convention for just the new folders versus not reorganizing source at all; rather than silently
picking one, that tradeoff was put to the person as a direct question before any file moved. The
git history itself is the record of this: `wayfinder/map.md`'s ticket resolutions and the ADRs in
`docs/adr/` predate the feature commits that depend on them, and the review-driven fix commits
(`fix: close ... gaps from Slice D/E review`) postdate the feature commits they correct.

## Where AI generated incorrect or suboptimal output, and how it was corrected

**1. Slice D (background jobs) review pass — two bugs in the GL-posting job.** The first
implementation pass formatted the simulated GL journal amount using `CultureInfo.InvariantCulture`'s
`"C"` format specifier. `InvariantCulture` has no real currency symbol, so a $5,000 reserve
rendered as `¤5,000.00` in the audit log instead of `$5,000.00` — a test assertion on the exact
audit description caught it immediately. The fix was to pin `CultureInfo.GetCultureInfo("en-US")`
explicitly rather than relying on whatever culture happens to be ambient on a Hangfire worker
thread, since that ambient culture must never leak into a permanent audit record's formatting.
Separately, the same job's idempotency backstop caught `DbUpdateConcurrencyException` and
unconditionally treated it as "a harmless race, someone else already posted this" — which is
wrong: a `RowVer` conflict fires for *any* concurrent modification of the row, not only a benign
double-post. The fix added an untracked re-read of `PostingStatus` inside the catch block to
distinguish an actual benign race (swallow it) from a genuine conflict that still needs to
propagate and be retried (rethrow it) — the original code would have silently swallowed real
conflicts it had no business treating as success.

**2. Slice E (document management) review pass — a real data-loss bug and a real availability
bug, both Critical.** The first implementation pass uploaded files to storage keyed only by their
submitted filename. Two uploads of `notes.txt` to the same claim silently overwrote the first
file with the second — undetected by the happy-path tests, which never uploaded two files with
the same name. The fix generates a unique filename (sanitized stem + the new document's own id)
before the upload, and additionally hardens both storage backends with create-only write guards
(`ETag.All` for Azure Blob, `FileMode.CreateNew` for the local-filesystem fallback) as a backstop
against the same failure mode recurring through a different code path later. Separately, the
committed default `appsettings.json` ships a blank Azure Blob connection string with
`StorageProvider: AzureBlob` as the literal setting — and the first implementation pass read that
setting literally, so every document upload or list call against the out-of-the-box configuration
threw and returned a `500`, directly violating `BR-D-03`'s "fall back to local filesystem if Azure
Blob Storage is not configured." The fix changed the `IStorageService` DI factory to check whether
Azure is *actually* configured (a non-blank connection string) and fall back to the local
filesystem provider regardless of what the `StorageProvider` string says, rather than trusting
that setting at face value.

**A smaller, lower-stakes example from the style-cleanup pass**: `dotnet format`'s automatic fix
for the `SYSLIB1045` suggestion (use `[GeneratedRegexAttribute]` instead of a runtime `Regex`)
mechanically generated a method literally named `MyRegex()`. Technically correct, but exactly the
kind of placeholder name that shouldn't survive into a commit — it was renamed to
`ClaimNumberPattern()` before committing. Minor, but a reminder that even deterministic tooling's
auto-fixes need a human/AI judgment pass, not blind acceptance.

## Honest assessment: where AI helped most, and where it didn't replace judgment

AI helped most on **repetitive, pattern-consistent surface area**: once the CQRS shape was
established for the first command (`CreateClaimCommand`), applying that exact shape — Command +
Handler + Validator, pipeline-validated, audit-logged, committed through the shared Unit of
Work — to the next nine commands was fast and consistent in a way that's genuinely hard for a
human to match by hand across that many files without small inconsistencies creeping in. The same
was true for EF Core Fluent configurations (one per entity, same conventions every time) and for
the test suite's structure.

AI helped second-most through **process, not raw generation**: the mandatory TDD cycle and the
fresh-model review at the end of every slice is what actually caught the bugs described above —
not because the implementing model couldn't write correct code, but because an implementer
reviewing its own just-written code shares its own blind spots. A fresh context with no
investment in the existing diff looked at `appsettings.json`'s defaults and asked "does this
actually fall back to local storage?" in a way the implementing pass, having just written the
`StorageProvider` check, didn't think to re-question.

Where AI did **not** replace judgment: every genuinely hard-to-reverse architecture decision in
this codebase (listed in `docs/adr/`) needed a human in the loop as the actual decision-maker, not
as an approver of an AI-proposed default — the role AI played there was laying out the considered
alternatives and their consequences, not picking one unilaterally. The clearest example from this
session: when asked to make the folder structure "neater," the honestly correct response wasn't
to silently pick a reorg scope and execute it — it was to recognize that the full-reorg-vs-light-
touch choice was a real, consequential tradeoff with no single right answer, and ask before moving
a single file.
