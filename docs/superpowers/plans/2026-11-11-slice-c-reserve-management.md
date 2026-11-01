# Slice C: Reserve Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement reserve component management on top of Slice A's schema and Slice B's plumbing: opening/adjusting reserves, the three-tier authority workflow, approve/reject/retract, self-approval prevention, the $10M aggregate-limit override, and the outbox write that bridges reserve approval into Slice D's (not-yet-built) GL posting job.

**Architecture:** Same CQRS/MediatR shape as Slice B. `OpenReserveCommand`'s handler, not a FluentValidation rule, decides the authority tier (needs the caller's role) and either auto-approves-and-applies-the-delta-immediately or leaves the transaction `PendingApproval` untouched. `ApproveReserveCommand`'s handler re-derives the same tier from the stored `ReserveHistory.Amount` to gate who may approve, applies the sequential-delta balance update (ADR-0002) and writes the `OutboxMessage` in the same `CommitChangesAsync()` call (ADR-0003/0004) as the approval itself.

**Tech Stack:** Same as Slice B — .NET 9, MediatR, FluentValidation, EF Core, xUnit + Testcontainers.

**Spec:** `specs/003-reserve-management.md`. Ground truth: `REQUIREMENTS.md` §6, §7.2, §8, §9.5–9.6, §10.2, §14.1. ADR-0002 (sequential-delta balance), ADR-0003 (repository/UoW), ADR-0004 (outbox), ADR-0005 (GL idempotency key format — this slice only *writes* the key, Slice D consumes it).

## Global Constraints

- `ClaimReserveComponents.CurrentAmount` only moves on a **final-approved** transaction (auto-approved at creation, or manually approved later) — never on `PendingApproval` or `Rejected` (ADR-0002).
- `IdempotencyKey` format is exactly `Reserve:{ReserveComponentId}:Change:{ChangeSequence}` (§6.5/ADR-0005) — `ChangeSequence` is monotonically increasing **per component**, starting at 1.
- The outbox write and the balance update happen in the same `CommitChangesAsync()` call as the approval that caused them — never two separate commits (ADR-0003/0004).
- A `PendingApproval` `ReserveHistory` row is never deleted and never gets a second resolution — once it moves to `Approved`/`Rejected`/`Cancelled` it's done; a revised amount is always a brand-new row (BR-R-04, user story 9).
- Every new entity follows Slice B's interceptor convention: handlers never set `OrganisationId`/`CreatedAt`/`UpdatedAt`/`UserCreated`/`UserModified` directly.
- Enums cross the API boundary as strings (`JsonStringEnumConverter`, already global from Slice B) — no new DTO introduces a raw enum field (Slice B hit this exact bug once already).

## Resolved Ambiguities

1. **"Set override flag" is a dedicated command, not folded into `TransitionClaimStatusCommand`.** The spec left this open deliberately. A status transition and a reserve-authority override are unrelated concerns; conflating them would make `TransitionClaimStatusCommandHandler`'s already-large switch statement worse for no benefit. New: `SetReserveOverrideCommand`, Manager-only, route `POST /api/claims/{id}/reserves/override`.
2. **The override flag is persistent (sticky) on the claim, not single-use.** BR-R-05 says a Manager must set it "before the reserve can be approved" — it doesn't say the flag is consumed on use. Once a Manager has signed off that a catastrophic claim may exceed $10M, later reserves on the *same* claim don't need a fresh override. Cheaper to implement and test than a consume-on-use design, and nothing in the spec contradicts it.
3. **The $10M aggregate check (BR-R-05) runs in *both* `OpenReserveCommandHandler`'s auto-approve branch and `ApproveReserveCommandHandler`**, not just manual approval. Many small ≤$10,000 auto-approved reserves can still cumulatively exceed $10M; the spec's own framing ("checked at approval time... since the limit is on *approved* totals") covers auto-approval too, since auto-approval *is* an approval that happens inline. A shared helper (`ReserveAggregateLimit.Evaluate`) backs both call sites.
4. **Authority-tier comparison uses `Math.Abs(amount)`.** `SubrogationRecoverable` can be negative; nothing in §6.3's table addresses sign, but a −$50,000 recovery is exactly as consequential as a +$50,000 indemnity reserve and should require the same oversight. Tiering by absolute value is the only reading that doesn't let a large negative transaction skip approval entirely.
5. **`ReserveHistory.PreviousBalance`/`NewBalance` are set twice for a `PendingApproval` row**: once at creation (a snapshot/projection using the component's balance *at submission time*), and again at approval time (re-read live, in case anything changed the component's balance in between, then applied for real). This isn't a BR-R-04 violation — BR-R-04/user story 11 protect the row from the *submitter* editing it while pending; the system completing its own lifecycle (status, approver, final balance numbers) on the same row is the row's designed resolution, identical in spirit to how `ApprovedByUserId`/`ApprovedAt` only get values at resolution time.
6. **No official `BR-*`/`CC-*` id exists for "this transaction isn't `PendingApproval` and can't be approved/rejected/retracted" or for the approver's role being insufficient for the amount tier.** Per Slice B's established precedent for unassigned ids (e.g. `TRANS-PENDINGPAYMENT-01`), this plan uses `RESERVE-NOT-PENDING-01` for the first and `BR-R-02` for the second (closest official id — §7.2's authority-threshold rule).
7. **`ListClaimReservesQuery`'s response is one object with both the current-balance summary and the full history**, matching §10.2's single `GET /api/claims/{id}/reserves` row ("Returns reserve summary... and full transaction history") rather than two separate endpoints.
8. **Reserve DTOs live in `Claims/Dtos/` and reuse Slice B's `ReserveSummaryDto`** (already shaped correctly: `ReserveComponentId, Component, CurrentAmount, Status`) rather than inventing a parallel type.

## Review Focus

1. **The $10,000/$100,000 authority-tier boundaries are off-by-one-prone** — exactly $10,000 must auto-approve, $10,000.01 must require Supervisor+; exactly $100,000 must require only Supervisor, $100,000.01 must require Manager. Both boundaries need explicit tests on both sides, not one comfortable value per tier.
2. **Self-approval must be rejected even when the self-approving user's role would otherwise be sufficient for the amount** — a Supervisor submitting their own $50,000 reserve and then trying to approve it must 422 on BR-R-03, not succeed because their role clears the tier check.
3. **A `PendingApproval` reserve must be provably immutable** — retract-then-resubmit is the only path to change it; approving, rejecting, or retracting a transaction that's already `Approved`/`Rejected`/`Cancelled` must 422, not silently no-op or succeed twice.
4. **The $10M override must actually gate approval, not just creation** — a reserve that would push the claim over $10M must 422 on *both* the auto-approve creation path (if its own amount pushes a near-$10M claim over) and the manual-approve path, and must succeed once (and only once Manager has) set the override.
5. **`OpenReserveCommand`'s "open" and "adjust" paths must share one component, not create a duplicate** — submitting a second `Indemnity` reserve on a claim that already has one must adjust the *same* `ClaimReserveComponent` row (summing via sequential delta), never insert a second `Indemnity` component for the same claim.

---

### Task 1: Schema — `Claim.ReserveOverrideApproved` + migration

**Files:**
- Modify: `backend/src/ClaimsModule.Domain/Claim.cs`
- Modify: `backend/src/ClaimsModule.Persistence/Configurations/ClaimConfiguration.cs`
- Create: EF Core migration via `dotnet ef migrations add AddReserveOverrideApprovedToClaims -p src/ClaimsModule.Persistence -s src/ClaimsModule.API`

**Interfaces:**
- Produces: `Claim.ReserveOverrideApproved : bool` (default `false`), consumed by Task 6 (`ApproveReserveCommandHandler`) and Task 4 (`OpenReserveCommandHandler`'s auto-approve branch) via the shared aggregate-limit helper from Task 3, and set by Task 7 (`SetReserveOverrideCommand`).

No dedicated test — this is a schema-only change; its behavior is exercised by Task 7's tests. Right-sized as its own task because it's the one step requiring Docker-independent verification (`dotnet ef migrations add` doesn't need a running database, but applying it does, and every later Docker-backed test implicitly re-proves the migration is valid).

- [ ] **Step 1: Add the property** — `public bool ReserveOverrideApproved { get; set; }` on `Claim`, with a one-line doc comment citing BR-R-05.
- [ ] **Step 2: Configure it** — `builder.Property(c => c.ReserveOverrideApproved).IsRequired().HasDefaultValue(false);` in `ClaimConfiguration`.
- [ ] **Step 3: Generate the migration**

Run: `dotnet ef migrations add AddReserveOverrideApprovedToClaims -p src/ClaimsModule.Persistence -s src/ClaimsModule.API` (from `backend/`)
Expected: a new migration file adding one `BIT NOT NULL DEFAULT 0` column to `Claims`, and `ClaimsDbContextModelSnapshot.cs` updated.

- [ ] **Step 4: `dotnet build`**
- [ ] **Step 5: Run `dotnet test tests/ClaimsModule.PersistenceTests`** (Docker) to confirm the migration applies cleanly from scratch against a fresh container.

Expected: all existing PersistenceTests still pass (the new column doesn't break anything already covered).

- [ ] **Step 6: Commit**

---

### Task 2: Repository extensions for reserves

**Files:**
- Modify: `backend/src/ClaimsModule.Application/IClaimReserveComponentRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/ClaimReserveComponentRepository.cs`

No dedicated tests — pure plumbing, exercised by every later task (same right-sizing rationale as Slice B's Task 5).

**Interfaces (produced, all `Task<T>` over `CancellationToken cancellationToken = default`):**

- `GetByIdAsync(Guid reserveComponentId) : ClaimReserveComponent?` — tracked, for approve/reject/retract's component lookup.
- `GetByClaimIdAndComponentAsync(Guid claimId, ReserveComponentType component) : ClaimReserveComponent?` — tracked; the "open-or-adjust" lookup Task 4 needs to decide whether to create a new component or reuse an existing one.
- `GetHistoryByIdAsync(Guid reserveHistoryId) : ReserveHistory?` — tracked, for approve/reject/retract's transaction lookup.
- `ListHistoryByClaimIdAsync(Guid claimId) : IReadOnlyList<ReserveHistory>` — for `ListClaimReservesQuery`'s full-history response, ordered `CreatedAt DESC` in the implementation.
- `GetNextChangeSequenceAsync(Guid reserveComponentId) : int` — `1 + count of existing ReserveHistory rows for that component` (or `1` if none exist yet).
- `GetTotalCurrentAmountByClaimIdAsync(Guid claimId) : decimal` — sum of `CurrentAmount` across all of the claim's reserve components (single-claim variant of the existing `GetTotalReservesByClaimIdsAsync`, used by the $10M check).
- `AddHistory(ReserveHistory history) : void` — adds to the tracked graph (mirrors `Add(ClaimReserveComponent)`), not saved until the caller's `CommitChangesAsync()`.
- `AddOutboxMessage(OutboxMessage message) : void` — same pattern; the outbox row belongs to the same atomic commit as the reserve-approval write that produced it (ADR-0004), so it's added here rather than via a separate repository.

- [ ] **Step 1: Add all eight members to `IClaimReserveComponentRepository`**
- [ ] **Step 2: Implement all eight in `ClaimReserveComponentRepository`** — `GetByIdAsync`/`GetByClaimIdAndComponentAsync`/`GetHistoryByIdAsync` via `FirstOrDefaultAsync`; `ListHistoryByClaimIdAsync` via `Where(...).OrderByDescending(h => h.CreatedAt).ToListAsync`; `GetNextChangeSequenceAsync` via `CountAsync(h => h.ReserveComponentId == id) + 1`; `GetTotalCurrentAmountByClaimIdAsync` via `Where(...).SumAsync(c => c.CurrentAmount)` (note: `SumAsync` over an empty sequence returns `0m`, not null, for a non-nullable `decimal` column — no null-coalescing needed); `AddHistory`/`AddOutboxMessage` via `dbContext.ReserveHistoryEntries.Add(...)`/`dbContext.OutboxMessages.Add(...)`.
- [ ] **Step 3: `dotnet build`**
- [ ] **Step 4: Commit**

---

### Task 3: Pure authority-tier and aggregate-limit logic

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ReserveAuthority.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/ReserveAggregateLimit.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/ReserveAuthorityTests.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/ReserveAggregateLimitTests.cs`

**Interfaces:**
- `ReserveAuthorityTier` enum: `Auto, Supervisor, Manager`.
- `ReserveAuthority.GetTier(decimal amount) : ReserveAuthorityTier` — tiers by `Math.Abs(amount)`: `<= 10_000m` → `Auto`; `<= 100_000m` → `Supervisor`; else → `Manager` (§6.3's table, Resolved Ambiguity 4).
- `ReserveAuthority.CanApprove(ReserveAuthorityTier tier, string? role) : bool` — `Auto` tier needs no approval call at all (callers shouldn't call this for `Auto`, but if they do: any authenticated role can "approve" it, i.e. `true`); `Supervisor` tier: role is `"supervisor"` or `"manager"` (case-insensitive); `Manager` tier: role is `"manager"` only.
- `ReserveAggregateLimit.Evaluate(decimal currentTotalAcrossComponents, decimal transactionAmount, bool overrideApproved) : IReadOnlyList<BlockingCondition>` — projected total = `currentTotalAcrossComponents + transactionAmount`; if `Math.Abs(projectedTotal) > 10_000_000m` and `!overrideApproved`, returns one `BlockingCondition("BR-R-05", "Total approved reserves will exceed $10,000,000. A Manager must set the override flag on this claim before this reserve can be approved.")`; else empty.

These are pure functions over primitives — handlers (Tasks 4, 6) compute the real numbers from repository data and pass them in, exactly like Slice B's `ClaimTransitionConditions`.

- [ ] **Step 1: Write failing `ReserveAuthorityTests`** — boundary theory covering: `10_000m` → `Auto`; `10_000.01m` → `Supervisor`; `100_000m` → `Supervisor`; `100_000.01m` → `Manager`; `-10_000.01m` (negative, `Math.Abs` > 10,000) → `Supervisor`; and `CanApprove` theory covering all 3 tiers × `"handler"`/`"supervisor"`/`"manager"`/`null` (9+ cases) including that `Manager` role satisfies the `Supervisor` tier.
- [ ] **Step 2: Run, verify fail**
- [ ] **Step 3: Implement `ReserveAuthorityTier`/`ReserveAuthority`**
- [ ] **Step 4: Run, verify pass**
- [ ] **Step 5: Write failing `ReserveAggregateLimitTests`** — exactly `10_000_000m` projected total → empty (not over the limit, `>` not `>=`); `10_000_000.01m` projected with `overrideApproved: false` → one `BR-R-05` condition; same projected total with `overrideApproved: true` → empty; a large negative projected total (e.g. `-10_000_001m`, from `Math.Abs`) with no override → also flagged (the limit is symmetric).
- [ ] **Step 6: Run, verify fail**
- [ ] **Step 7: Implement `ReserveAggregateLimit`**
- [ ] **Step 8: Run, verify pass**
- [ ] **Step 9: Commit**

---

### Task 4: `OpenReserveCommand`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/OpenReserve/OpenReserveCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/OpenReserve/OpenReserveCommandValidator.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/OpenReserve/OpenReserveCommandHandler.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ReserveTransactionDto.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/OpenReserveTests.cs`

**Interfaces:**
- `OpenReserveCommand : IRequest<ReserveTransactionDto> { Guid ClaimId; ReserveComponentType Component; decimal Amount; string ChangeReason; ReserveTransactionType TransactionType; }`.
- `ReserveTransactionDto { Guid ReserveHistoryId; Guid ReserveComponentId; string Component; string TransactionType; decimal Amount; decimal PreviousBalance; decimal NewBalance; string ApprovalStatus; Guid? ApprovedByUserId; DateTimeOffset? ApprovedAt; Guid? RejectedByUserId; DateTimeOffset? RejectedAt; string? RejectionReason; string ChangeReason; string PostingStatus; Guid? SubmittedByUserId; DateTimeOffset CreatedAt; }` (shared response shape for every reserve-mutating command in this slice — Tasks 4, 6, 7, 8 all return this).
- Consumes: Task 2's repository methods; Task 3's `ReserveAuthority`/`ReserveAggregateLimit`; Slice B's `IClaimRepository.GetByIdAsync`, `IAuditLogService`, `ICurrentUserService`, `NotFoundException`, `BusinessRuleViolationException`.

**Validator rules:**

| Property | Rule | Message |
|---|---|---|
| `Amount` | `!= 0`, and `> 0` unless `Component == SubrogationRecoverable` | `"Reserve amount must be greater than zero."` |
| `ChangeReason` | `NotEmpty` | basic, no `BR-*` id |
| (cross-field) `ClaimId` | the claim's `PolicyId` must not be null (`MustAsync` via `IClaimRepository.GetByIdAsync`) | `"No policy linked. Policy must be associated before reserves can be set."` — **`Severity.Error`** here (hard block), unlike Slice B's identical message which was a Warning at claim-creation time — BR-C-06's consequence clause makes this specific case a block (user story 13) |

**Handler steps:**

1. `claim = await claimRepository.GetByIdAsync(request.ClaimId, ct) ?? throw NotFoundException("Claim", request.ClaimId);` (the validator already proved `PolicyId` isn't null, but the claim itself could still 404 if deleted between validation and handling — keep the check).
2. `component = await reserveComponentRepository.GetByClaimIdAndComponentAsync(request.ClaimId, request.Component, ct);`
3. If `component is null`: create a new one — `component = new ClaimReserveComponent { ReserveComponentId = Guid.NewGuid(), ClaimId = request.ClaimId, Component = request.Component, CurrentAmount = 0m, Status = ReserveComponentStatus.Active };` then `reserveComponentRepository.Add(component);` (client-side id assignment, same rationale as Slice B's `Claim.ClaimId` — the history row below needs a real id in the same `SaveChanges`).
4. `var previousBalance = component.CurrentAmount; var newBalance = previousBalance + request.Amount; var tier = ReserveAuthority.GetTier(request.Amount);`
5. If `tier == Auto`: `var currentTotal = await reserveComponentRepository.GetTotalCurrentAmountByClaimIdAsync(request.ClaimId, ct); var limitConditions = ReserveAggregateLimit.Evaluate(currentTotal, request.Amount, claim.ReserveOverrideApproved); if (limitConditions.Count > 0) throw new BusinessRuleViolationException(limitConditions);` (Resolved Ambiguity 3 — checked here, not only in `ApproveReserveCommand`).
6. `var sequence = await reserveComponentRepository.GetNextChangeSequenceAsync(component.ReserveComponentId, ct); var idempotencyKey = $"Reserve:{component.ReserveComponentId}:Change:{sequence}";`
7. Build `history = new ReserveHistory { ReserveHistoryId = Guid.NewGuid(), ClaimId = request.ClaimId, ReserveComponentId = component.ReserveComponentId, TransactionType = request.TransactionType, Amount = request.Amount, PreviousBalance = previousBalance, NewBalance = newBalance, ChangeReason = request.ChangeReason, SubmittedByUserId = currentUserService.UserId, ChangeSequence = sequence, IdempotencyKey = idempotencyKey, PostingStatus = ReservePostingStatus.Pending, ApprovalStatus = tier == Auto ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.PendingApproval };` then `reserveComponentRepository.AddHistory(history);`
8. If `tier == Auto`: `component.CurrentAmount = newBalance;` and write the outbox row: `reserveComponentRepository.AddOutboxMessage(new OutboxMessage { OutboxMessageId = Guid.NewGuid(), Type = "EnqueueGLPostingJob", Payload = JsonSerializer.Serialize(new { history.ReserveHistoryId, history.IdempotencyKey }) });` (Resolved Ambiguity from spec's own Implementation Decisions — balance update and outbox write in the same commit).
9. `await auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "RESERVE_CREATED", $"Reserve transaction submitted for {request.Component} ({request.Amount:C}).", null, null, history.ReserveHistoryId, "ReserveHistory"), ct);`
10. If `tier == Auto`: also `await auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "RESERVE_AUTO_APPROVED", $"Reserve auto-approved: {request.Component} ({request.Amount:C}).", null, null, history.ReserveHistoryId, "ReserveHistory"), ct);`
11. `await reserveComponentRepository.CommitChangesAsync(ct);` — single `SaveChangesAsync`.
12. Map `history` → `ReserveTransactionDto` by hand (same style as Slice B's `GetClaimDetailQuery` — no AutoMapper for this one, per Resolved Ambiguity about mixed mapping style already accepted in Slice B's final review) and return.

Route: `POST /api/claims/{id}/reserves`, `[Authorize]`, 201 with the `ReserveTransactionDto` body (no `Location` header — there's no single-transaction GET endpoint in this slice's scope, only the list).

- [ ] **Step 1: Write failing `OpenReserveTests`** — one `[Fact]` per row:
  - Happy path ≤$10,000 → 201, `ApprovalStatus == "AutoApproved"`, `ClaimReserveComponents.CurrentAmount` for that claim/component reflects the new balance (verify via a follow-up `GET /api/claims/{id}` and checking `Reserves`).
  - Exactly `$10,000.00` → `AutoApproved` (Review Focus #1).
  - `$10,000.01` → `PendingApproval`, balance unchanged (still 0).
  - Exactly `$100,000.00` → `PendingApproval` (not yet Manager-only at the boundary itself — this just proves the row is created correctly; the *approval* role-boundary is Task 6's concern).
  - BR-R-01: `Amount = -500` on `Indemnity` → 422 field error on `Amount`; `Amount = -500` on `SubrogationRecoverable` → 201 (negative is allowed for that component).
  - A second `Indemnity` submission on the same claim adjusts the *same* component (Review Focus #5) — submit twice, assert only one `ClaimReserveComponent` row exists for that claim+component via `GET /api/claims/{id}` (`Reserves.Count == 1`) and its `CurrentAmount` reflects both deltas summed.
  - BR-C-06 consequence: a claim created with `PolicyId = null` → `POST .../reserves` → 422 (not a Warning) with the "No policy linked..." message.
  - Review Focus #4 (creation half): submit two auto-approving reserves whose combined total would exceed $10M on a claim without the override set → second one 422 `BR-R-05`; same scenario after `SetReserveOverrideCommand` (stub this call directly via a raw SQL `UPDATE Claims SET ReserveOverrideApproved = 1` for *this* task only, since `SetReserveOverrideCommand` doesn't exist until Task 7 — note this explicitly in the test as a forward-reference workaround, and revisit once Task 7 lands to use the real endpoint instead; simplest to just leave the SQL-based version, it's equally valid as a test of the handler logic) → 201.

- [ ] **Step 2: Run, verify fail**
- [ ] **Step 3: Implement `OpenReserveCommand`, `OpenReserveCommandValidator`, `OpenReserveCommandHandler`, `ReserveTransactionDto`, `ClaimsController.OpenReserve`**
- [ ] **Step 4: Run, verify pass**
- [ ] **Step 5: Commit**

---

### Task 5: `ListClaimReservesQuery`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ListClaimReservesQuery.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ReserveListDto.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ListClaimReservesTests.cs`

**Interfaces:**
- `ListClaimReservesQuery(Guid ClaimId) : IRequest<ReserveListDto>`.
- `ReserveListDto { List<ReserveSummaryDto> Components; List<ReserveTransactionDto> History; }` (Resolved Ambiguity 7 — one combined response; reuses Slice B's `ReserveSummaryDto` and Task 4's `ReserveTransactionDto`).
- Consumes: `IClaimRepository.GetByIdAsync` (404 check), `IClaimReserveComponentRepository.ListByClaimIdAsync` + `ListHistoryByClaimIdAsync`.

Route: `GET /api/claims/{id}/reserves`, `[Authorize]`.

- [ ] **Step 1: Write failing tests**: a claim with one auto-approved and one pending reserve → `GET .../reserves` returns `Components` with the right `CurrentAmount` (only the auto-approved one moved it) and `History` with both transactions, newest first; `GET` for an unknown claim → 404.
- [ ] **Step 2: Run, verify fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run, verify pass**
- [ ] **Step 5: Commit**

---

### Task 6: `ApproveReserveCommand`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ApproveReserve/ApproveReserveCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/ApproveReserve/ApproveReserveCommandHandler.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ApproveReserveTests.cs`

**Interfaces:**
- `ApproveReserveCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<ReserveTransactionDto>`.
- Consumes: Task 2's `GetHistoryByIdAsync`/`GetByIdAsync`/`GetTotalCurrentAmountByClaimIdAsync`/`AddOutboxMessage`; Task 3's `ReserveAuthority.GetTier`/`CanApprove` and `ReserveAggregateLimit.Evaluate`.

**Handler steps:**

1. `history = await repo.GetHistoryByIdAsync(request.ReserveHistoryId, ct) ?? throw NotFoundException("ReserveHistory", request.ReserveHistoryId);` — also confirm `history.ClaimId == request.ClaimId`, else `NotFoundException` too (a transaction id that exists but belongs to a different claim is "not found" from this route's perspective, not a 403/mismatch case).
2. `if (history.ApprovalStatus != PendingApproval) throw new BusinessRuleViolationException([new BlockingCondition("RESERVE-NOT-PENDING-01", "Only a reserve transaction in PendingApproval status can be approved.")]);`
3. `if (currentUserService.UserId is not null && currentUserService.UserId == history.SubmittedByUserId) throw new BusinessRuleViolationException([new BlockingCondition("BR-R-03", "Self-approval is not permitted.")]);` (BR-R-03, Review Focus #2 — checked *before* the role-tier check, so a Supervisor can't route around it by having a sufficient role).
4. `var tier = ReserveAuthority.GetTier(history.Amount); if (!ReserveAuthority.CanApprove(tier, currentUserService.Role)) throw new BusinessRuleViolationException([new BlockingCondition("BR-R-02", "Your role does not have authority to approve this reserve amount.")]);`
5. `claim = await claimRepository.GetByIdAsync(request.ClaimId, ct) ?? throw NotFoundException("Claim", request.ClaimId);`
6. `var currentTotal = await repo.GetTotalCurrentAmountByClaimIdAsync(request.ClaimId, ct); var limitConditions = ReserveAggregateLimit.Evaluate(currentTotal, history.Amount, claim.ReserveOverrideApproved); if (limitConditions.Count > 0) throw new BusinessRuleViolationException(limitConditions);`
7. `component = await repo.GetByIdAsync(history.ReserveComponentId, ct)!;` (guaranteed to exist — the FK is `Restrict`, it can't have been deleted).
8. Recompute live: `var previousBalance = component.CurrentAmount; var newBalance = previousBalance + history.Amount; history.PreviousBalance = previousBalance; history.NewBalance = newBalance; component.CurrentAmount = newBalance;` (Resolved Ambiguity 5).
9. `history.ApprovalStatus = Approved; history.ApprovedByUserId = currentUserService.UserId; history.ApprovedAt = DateTimeOffset.UtcNow;`
10. `repo.AddOutboxMessage(new OutboxMessage { OutboxMessageId = Guid.NewGuid(), Type = "EnqueueGLPostingJob", Payload = JsonSerializer.Serialize(new { history.ReserveHistoryId, history.IdempotencyKey }) });`
11. `await auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "RESERVE_APPROVED", $"Reserve approved: {component.Component} ({history.Amount:C}).", null, null, history.ReserveHistoryId, "ReserveHistory"), ct);`
12. `await repo.CommitChangesAsync(ct);`
13. Map and return `ReserveTransactionDto`.

Route: `POST /api/claims/{id}/reserves/{txnId}/approve`, `[Authorize]`.

- [ ] **Step 1: Write failing `ApproveReserveTests`** — one `[Fact]` per row:
  - Full manual-approve path: submit as `handler` (amount in the Supervisor tier), approve as a *different* `supervisor` user → 200, `ApprovalStatus == "Approved"`, component balance updated, an `OutboxMessage` row exists for this `ReserveHistoryId` (query directly via SQL against `fixture.ClaimsConnectionString`, same pattern as Slice B's `StatusTransitionTests` raw-SQL helpers).
  - Self-approval (Review Focus #2): submit and approve as the *same* `supervisor` user → 422, `blockingConditions[0].code == "BR-R-03"`.
  - Role-tier insufficient: submit a Manager-tier amount (>$100,000), attempt approval as `supervisor` → 422 `BR-R-02`; approve the same as `manager` → 200.
  - Exactly `$100,000.00` approved by `supervisor` → 200 (boundary, Review Focus #1); `$100,000.01` approved by `supervisor` → 422 `BR-R-02`, approved by `manager` → 200.
  - Immutability (Review Focus #3): approve a transaction, then attempt to approve it again → 422 `RESERVE-NOT-PENDING-01`.
  - $10M override (Review Focus #4, approval half): drive a claim's approved total close to $10M via two auto-approved $10,000 reserves plus one Supervisor-tier manual approval reaching just under the limit, then submit and attempt to approve one more that would cross it → 422 `BR-R-05`; set the override via direct SQL (`UPDATE Claims SET ReserveOverrideApproved = 1` — same forward-reference note as Task 4, superseded once Task 7's real endpoint exists) → approve again → 200.
  - Approving a transaction for a `txnId` that belongs to a *different* claim than the one in the route → 404.

- [ ] **Step 2: Run, verify fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run, verify pass**
- [ ] **Step 5: Commit**

---

### Task 7: `RejectReserveCommand`, `RetractReserveCommand`, `SetReserveOverrideCommand`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/RejectReserve/RejectReserveCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/RejectReserve/RejectReserveCommandHandler.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/RetractReserve/RetractReserveCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/RetractReserve/RetractReserveCommandHandler.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/SetReserveOverride/SetReserveOverrideCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/SetReserveOverride/SetReserveOverrideCommandHandler.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/RejectReserveTests.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/RetractReserveTests.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/SetReserveOverrideTests.cs`

Bundled into one task: each of the three commands is small (no balance math, no outbox write) and the plan's own task-right-sizing guidance folds genuinely small, related units together rather than creating three near-empty tasks.

**Interfaces:**
- `RejectReserveCommand(Guid ClaimId, Guid ReserveHistoryId, string RejectionReason) : IRequest<ReserveTransactionDto>`.
- `RetractReserveCommand(Guid ClaimId, Guid ReserveHistoryId) : IRequest<ReserveTransactionDto>`.
- `SetReserveOverrideCommand(Guid ClaimId) : IRequest` (no response payload needed — `Unit`).

**`RejectReserveCommandHandler`:** load history (same `ClaimId` cross-check + 404 as Task 6 step 1) → must be `PendingApproval` (else `RESERVE-NOT-PENDING-01`) → role check: `role` must be `"supervisor"` or `"manager"` (case-insensitive — *no* amount-tier distinction for rejection, unlike approval; any Supervisor or Manager can reject any pending amount) → else `BusinessRuleViolationException([new BlockingCondition("BR-R-02", "Your role does not have authority to reject this reserve.")])` → set `ApprovalStatus = Rejected`, `RejectedByUserId`, `RejectedAt`, `RejectionReason` → audit `RESERVE_REJECTED` (`OldValue` = null, `NewValue` = the rejection reason) → commit → return DTO. No balance change, no outbox write (rejected reserves never post to GL).

**`RetractReserveCommandHandler`:** load history + 404 check → must be `PendingApproval` → must be the *submitter* (`currentUserService.UserId == history.SubmittedByUserId`; a mismatch is a different person trying to retract someone else's pending reserve → `BusinessRuleViolationException([new BlockingCondition("RESERVE-NOT-PENDING-01", "Only the submitter may retract their own pending reserve.")])` — reusing the same code since it's still "this isn't yours to resolve," not a new named rule) → set `ApprovalStatus = Cancelled` → audit `RESERVE_RETRACTED` → commit → return DTO.

**`SetReserveOverrideCommandHandler`:** load claim + 404 check → role must be `"manager"` (else `BusinessRuleViolationException([new BlockingCondition("BR-R-05", "Only a Manager may set the reserve override flag.")])`) → `claim.ReserveOverrideApproved = true;` → audit `RESERVE_OVERRIDE_SET` (not in §14.1's official list — a reasonable addition for traceability, same judgment call Slice B made for its own unassigned-code cases) → `claimRepository.CommitChangesAsync(ct);` — note this uses `IClaimRepository`, not `IClaimReserveComponentRepository` (it's a `Claim` mutation, not a reserve one).

Routes: `POST /api/claims/{id}/reserves/{txnId}/reject` (200, body), `POST /api/claims/{id}/reserves/{txnId}/retract` (200, body), `POST /api/claims/{id}/reserves/override` (204, no body). All `[Authorize]`.

- [ ] **Step 1: Write failing tests for all three** (one file each, per the Files list above):
  - Reject: happy path by `supervisor` → 200, `ApprovalStatus == "Rejected"`, balance unchanged; by `handler` → 422 `BR-R-02`; re-rejecting an already-rejected transaction → 422 `RESERVE-NOT-PENDING-01`.
  - Resubmit after rejection (user story 9): reject a transaction, then submit a brand-new `OpenReserveCommand` for the *same* component → 201, a distinct new `ReserveHistoryId`; `GET .../reserves` shows both rows (the `Rejected` one still present, unmodified) via its `History` list.
  - Retract: happy path by the original `handler` submitter → 200, `ApprovalStatus == "Cancelled"`; by a *different* user (even a supervisor) → 422; retracting an already-approved transaction → 422.
  - Override: set by `manager` → 204 (or 200 — pick one and assert it), then `GET /api/claims/{id}` shows the flag if exposed in `ClaimDetailDto` (extend `ClaimDetailDto` with `bool ReserveOverrideApproved` as part of this task, since this is the first place it needs to be externally observable); set by `handler`/`supervisor` → 422.
- [ ] **Step 2: Run, verify fail**
- [ ] **Step 3: Implement all three commands/handlers + the `ClaimDetailDto.ReserveOverrideApproved` field + the three controller actions**
- [ ] **Step 4: Run, verify pass**
- [ ] **Step 5: Go back to Task 4's and Task 6's tests that used a raw-SQL `UPDATE Claims SET ReserveOverrideApproved = 1` workaround and switch them to call the real `POST .../reserves/override` endpoint instead, now that it exists.** Re-run those two test files to confirm they still pass through the real endpoint.
- [ ] **Step 6: Commit**

---

### Task 8: Re-verify Slice B's closure/transition conditions against real reserve data

**Files:**
- Modify (add cases to, don't rewrite): `backend/tests/ClaimsModule.ApiIntegrationTests/StatusTransitionTests.cs`

This is the spec's own "Further Notes" ask: Slice B's CC-01 and Open→PendingPayment tests used hand-inserted raw-SQL `ReserveHistory` rows (since no endpoint existed yet to create real ones). Now one does — add the real-endpoint equivalents alongside (not replacing) the existing SQL-based tests, which stay as cheap, fast, narrowly-scoped unit-level proofs of the condition logic itself.

- [ ] **Step 1: Write failing tests**:
  - `Open→Closed` is blocked by CC-01 when a *real* `OpenReserveCommand` left a reserve `PendingApproval` (amount in the Supervisor tier, never approved) — submit via `POST .../reserves`, then attempt `PUT .../status` to `Closed` → 422 `CC-01`.
  - `Open→PendingPayment` succeeds once a *real* reserve has been through the full submit-then-approve flow (auto-approved, since ≤$10,000 is simplest) — no more raw-SQL `INSERT INTO ReserveHistory` needed for this one case.
  - `Closed` succeeds once that same reserve is genuinely `Approved` and the claim is otherwise clean (no `PendingApproval` reserves remaining).
- [ ] **Step 2: Run, verify fail or pass** — these may well pass immediately if Tasks 1–7 are correct, since they're exercising already-implemented logic through a new path; that's fine, it's still the right place to pin this down with a real test rather than trusting it by inspection.
- [ ] **Step 3: If anything fails, use systematic-debugging to find whether the bug is in Slice B's closure-condition logic or Slice C's reserve-writing logic, then fix it.**
- [ ] **Step 4: Commit**

---

### Final steps (not a plan task — part of the `/implement` workflow wrapping this plan)

- [ ] Run `dotnet build` and `dotnet test tests/ClaimsModule.UnitTests` after every task (fast loop).
- [ ] Run the full `dotnet test` (Docker required) once after Task 8.
- [ ] Dispatch a fresh whole-branch code review (most capable model), same process as Slice B's final review.
- [ ] Commit (already happening per-task above) — no separate final commit needed unless the review produces fixups.
