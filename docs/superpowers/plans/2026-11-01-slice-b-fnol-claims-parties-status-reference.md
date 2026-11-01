# Slice B: FNOL, Claim Reads, Parties, Status Transitions, Reference Data — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement FNOL intake, claim reads, party management, status-transition state machine, and reference-data/policy-search endpoints — and the shared request-pipeline infrastructure (real JWT auth, severity-aware validation pipeline, exception-to-status-code middleware, centralized audit logging, correlation-id propagation) every one of them needs.

**Architecture:** Clean Architecture / CQRS via MediatR. Commands write through per-aggregate repositories (`IClaimRepository`, `IClaimReserveComponentRepository`, extended this slice with query methods) + a shared `IAuditLogService`; one `SaveChangesAsync` per request keeps claim + children + audit rows atomic. Queries read through the same repository interfaces (extended with list/get methods) — no direct EF Core usage from `Application`. A generic `ValidationBehavior<TRequest,TResponse>` MediatR pipeline behavior blocks only on FluentValidation `Severity.Error`; `Severity.Warning` failures attach to the command and get audited by the handler once a `ClaimId` exists. One exception-handling middleware maps exception type → HTTP status.

**Tech Stack:** .NET 9 / C# 13, MediatR 14, FluentValidation 12, AutoMapper 16, EF Core 9 (SQL Server), `Microsoft.AspNetCore.Authentication.JwtBearer` (new — pin to `9.0.x`), xUnit + Testcontainers (`ClaimsModule.ApiIntegrationTests`), NetArchTest (`ArchitectureTests`).

**Spec:** `specs/002-fnol-claims-parties-status-reference.md` (this slice's own spec), `REQUIREMENTS.md` §3–10, §14, §16, ADR-0001, ADR-0003, ADR-0006, ADR-0007, ADR-0008.

## Global Constraints

- Central package management: add new packages via `dotnet add <project> package <Name> --version 9.0.x` for ASP.NET Core packages (never an inline `Version=`).
- `Domain` and `Application` must never reference EF Core, Hangfire, or Azure SDK types directly (`ArchitectureTests.cs` enforces this at build time) — all new query capability goes through repository interfaces defined in `Application`, implemented in `Persistence`.
- All money: `DECIMAL(19,4)`. All timestamps: `DATETIMEOFFSET(7)` UTC. GUID PKs: `NEWSEQUENTIALID()` default (one deliberate exception below for `Claim.ClaimId`).
- `OrganisationId` tenant scoping and soft-delete are enforced by `ClaimsDbContext`'s existing global query filter — do not add ad-hoc `WHERE IsDeleted = 0` clauses anywhere.
- No hardcoded GUIDs in application logic (NFR-CONST-04) — read `SeedData.OrganisationId` only from `ClaimsDbContext.CurrentOrganisationId`, never inline.
- `IAuditLogService` is the only write path to `ClaimAuditLog` (AUD-02) — no handler ever calls `dbContext.ClaimAuditLogEntries.Add` directly.
- Enums cross the API boundary as strings, not numbers — `Program.cs` must register `JsonStringEnumConverter` (step added in Task 4; this is new, nothing registers it today).
- 422 is reserved for validation/business-rule failures only (ADR-0006) — never introduce a 403; a role-authority failure is a 422 `blockingConditions` entry, matching the existing pattern for reserve-approval role checks in §8.

## Resolved Ambiguities (decisions this plan locks in)

These are places where `REQUIREMENTS.md`'s prose and `specs/002-...md`'s user stories don't quite line up, or where `specs/002` left an implementation detail open. Each is a deliberate call, not an oversight:

1. **BR-C-01/03/05/07 block claim *creation* outright (422, nothing persisted)**, not just the later Draft→Open transition. `REQUIREMENTS.md` §4.4/§7.1/§8 describe these as blocking "the transition," but `specs/002` user stories 2–7 explicitly say creation itself is rejected with 422 for all four, and that's the only reading consistent with the generic `ValidationBehavior` (which blocks the whole command on `Severity.Error`). BR-C-02 (policy date range) and BR-C-06 (no policy) are `Severity.Warning` — creation succeeds, claim persists into `Draft`, each warning becomes a `VALIDATION_ISSUE_ADDED` audit entry. BR-ST-02 (Draft→Open: no Critical issues + ≥1 Claimant) is still implemented and unit-tested as defense-in-depth even though it's unreachable via `POST /api/claims` alone today.
2. **No initial reserve in `CreateClaimCommand`.** User story 1 mentions an "optional initial reserve," but the spec's own Out of Scope section is explicit: "no command in this slice writes to [the reserve] tables." The Out of Scope line wins — `CreateClaimCommand` has no reserve fields at all; initial-reserve-at-intake is Slice C's job.
3. **CC-01 and the Open→PendingPayment "approved reserve" condition read `ReserveHistory.ApprovalStatus`, not `ClaimReserveComponent.Status`.** `ClaimReserveComponentStatus` only has `Active`/`Closed` values — there's no `PendingApproval` component status. "No reserve components with status PendingApproval remain" (CC-01) and "at least one reserve component with status Approved" (Open/UnderInvestigation→PendingPayment) both actually describe the *latest relevant `ReserveHistory` row's* `ApprovalStatus` for the claim's reserves, not a `ClaimReserveComponent.Status` value.
4. **`Claim.ClaimId` is assigned client-side (`Guid.NewGuid()`) in `CreateClaimCommandHandler`, not left for `NEWSEQUENTIALID()`.** This lets the audit entries (`CLAIM_CREATED`, `VALIDATION_ISSUE_ADDED`) reference the real `ClaimId` in the *same* `SaveChangesAsync` call as the claim+children insert, keeping FNOL intake a single atomic transaction without introducing a new cross-cutting transaction API into handlers (which ADR-0003 forbids injecting `IUnitOfWork` into directly). EF Core uses an explicitly-set non-default GUID over the `HasDefaultValueSql` default automatically — no config change needed. Every other entity in the graph keeps relying on the existing navigation-collection backfill.
5. **Closed→Reopened is a single handler call that ends with `Status = Open`.** BR-ST-04 says "the claim immediately transitions to Open after Reopened." The handler writes two `STATUS_CHANGED` audit rows (`Closed→Reopened`, then `Reopened→Open`) plus one `CLAIM_REOPENED` row, but only ever persists the final `Open` status — `Reopened` is never a durably queryable row state. `ListClaimStatusesQuery` still reports `Reopened → [Open]` in its transition table (matching §4.2 verbatim) since that's about documenting the state machine, not what's reachable through this one cascading call.
6. **BR-ST-04's missing-role/missing-reason checks return 422 `blockingConditions`**, not 403 — consistent with ADR-0006, which defines no 403 case anywhere, and with §8's existing pattern of using 422 for role-authority failures ("Your role does not have authority...").
7. **The Open/UnderInvestigation→PendingPayment reserve condition has no assigned `BR-*`/`CC-*` id in `REQUIREMENTS.md`.** Use the literal code `"TRANS-PENDINGPAYMENT-01"` for its `blockingConditions` entry (documented inline in the handler) rather than inventing a fake `BR-*` id.
8. **`DbUpdateConcurrencyException` → 409 is wired in the middleware but not exercised by a test in this slice.** None of this slice's write endpoints take a `RowVer`/`If-Match` input, so there's no spec'd way to force a genuine conflict through the HTTP surface yet. Flagged in Review Focus below as a known, deliberate gap — not silently skipped.

## Review Focus

1. **Concurrent FNOL submissions must never collide on `ClaimNumber`** (FR-CLN-02) — fire N parallel `POST /api/claims` calls, assert N distinct, correctly-formatted claim numbers.
2. **Removing a non-last `Claimant` must succeed; only removing the *last active* `Claimant` blocks (BR-P-01)** — a claim with two Claimants must allow removing one.
3. **CC-04 (open reserves at closure) is a soft block that a `justificationNote` lifts, not a hard block** — closing with a nonzero reserve and no note → 422; the same request with a note → succeeds.
4. **An inactive `CauseOfLossCode` and a nonexistent one must both 422, and a valid active one must not** — the three-way boundary is easy to get only two-thirds right.
5. **Role enforcement on `Reopened` is real, not just a UI nicety** — a `handler`-role token attempting `Closed→Reopened` must be rejected with 422 even though the transition would otherwise be well-formed.

---

### Task 1: Harden `ApiFixture` for inter-test isolation

**Files:**
- Modify: `backend/tests/ClaimsModule.ApiIntegrationTests/ApiFixture.cs`
- Create: `backend/tests/ClaimsModule.ApiIntegrationTests/ApiTestBase.cs`

**Interfaces:**
- Produces: `ApiFixture.ClaimsConnectionString` (public `string` property, mirrors the existing private `_claimsConnectionString`), `ApiFixture.ResetAsync()` (public `Task`, Respawn-based, mirrors `PersistenceTests/DatabaseFixture.ResetAsync`), `ApiTestBase(ApiFixture fixture) : IAsyncLifetime` whose `InitializeAsync` calls `fixture.ResetAsync()` — every later test class in this slice inherits/uses this instead of implementing its own reset.

Every write-oriented test class created in later tasks must derive from (or otherwise call) `ApiTestBase`'s reset — note this requirement in each later task rather than repeating the mechanism.

- [ ] **Step 1: Add `Respawner`/`SqlConnection` fields and `ResetAsync()` to `ApiFixture`, modeled exactly on `DatabaseFixture.ResetAsync`**

`TablesToIgnore`: `CauseOfLossCodes`, `Policies` (migration-seeded reference data must survive a reset).

- [ ] **Step 2: Expose `public string ClaimsConnectionString => _claimsConnectionString;`**

- [ ] **Step 3: Write `ApiTestBase`**

```csharp
public abstract class ApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
```

- [ ] **Step 4: Verify isolation with a throwaway two-test check**

Temporarily add two `[Fact]`s to a scratch test class deriving `ApiTestBase` that each `POST /api/claims`-shaped raw SQL insert a dummy row into an existing ignorable-adjacent table... Actually simplest real check: add two `[Fact]`s asserting `SELECT COUNT(*) FROM Policies` stays at 5 across both tests (proves `TablesToIgnore` works) while a scratch insert into, e.g., `ClaimAuditLog` in test 1 doesn't leak into test 2. Delete the scratch class once verified — this step is diagnostic, not a permanent test.

Run: `dotnet test tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~ScratchIsolation"`
Expected: PASS, then delete the scratch file.

- [ ] **Step 5: Commit**

```bash
git add backend/tests/ClaimsModule.ApiIntegrationTests/ApiFixture.cs backend/tests/ClaimsModule.ApiIntegrationTests/ApiTestBase.cs
git commit -m "test: add Respawn-based reset and connection string to ApiFixture"
```

---

### Task 2: Exception-handling middleware (ADR-0006)

**Files:**
- Create: `backend/src/ClaimsModule.Application/Common/Exceptions/NotFoundException.cs`
- Create: `backend/src/ClaimsModule.Application/Common/Exceptions/BusinessRuleViolationException.cs`
- Create: `backend/src/ClaimsModule.Application/Common/Exceptions/BlockingCondition.cs`
- Create: `backend/src/ClaimsModule.API/Middleware/ExceptionHandlingMiddleware.cs`
- Create: `backend/src/ClaimsModule.API/Middleware/ErrorResponse.cs`
- Modify: `backend/src/ClaimsModule.API/Program.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/ExceptionHandlingMiddlewareTests.cs`

**Interfaces:**
- Produces: `NotFoundException(string entityName, object id)` (message: `"{entityName} '{id}' was not found."`); `BlockingCondition(string Code, string Message)` (record); `BusinessRuleViolationException(IReadOnlyList<BlockingCondition> BlockingConditions)` — both consumed by every handler from Task 8 onward. `ErrorResponse { string Type; string Title; int Status; Dictionary<string,string[]>? Errors; IReadOnlyList<BlockingCondition>? BlockingConditions; }`, serialized camelCase.
- Consumes: `FluentValidation.ValidationException` (its `.Errors` is `IEnumerable<ValidationFailure>`), `Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException`.

Mapping table the middleware implements:

| Exception | Status | `type` | Body |
|---|---|---|---|
| `FluentValidation.ValidationException` | 422 | `ValidationError` | `errors` dict, grouped by `PropertyName`, values = `ErrorMessage` list |
| `BusinessRuleViolationException` | 422 | `BusinessRuleViolation` | `blockingConditions` array |
| `NotFoundException` | 404 | `NotFound` | neither `errors` nor `blockingConditions` |
| `DbUpdateConcurrencyException` | 409 | `ConcurrencyConflict` | neither |
| anything else | 500 | `InternalServerError` | neither |

`title` for 422 ValidationError: `"One or more validation errors occurred."` (verbatim from §10.5). Pick a plain one-line `title` for the other four.

- [ ] **Step 1: Write the failing middleware tests**

```csharp
public class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(typeof(FluentValidation.ValidationException), 422, "ValidationError")]
    [InlineData(typeof(NotFoundException), 404, "NotFound")]
    [InlineData(typeof(DbUpdateConcurrencyException), 409, "ConcurrencyConflict")]
    [InlineData(typeof(InvalidOperationException), 500, "InternalServerError")]
    public async Task Maps_exception_type_to_status_and_type(Type exceptionType, int expectedStatus, string expectedType) { ... }

    [Fact]
    public async Task BusinessRuleViolationException_maps_to_422_with_blockingConditions() { ... }

    [Fact]
    public async Task ValidationException_errors_are_grouped_by_property_name() { ... }
}
```

Construct each exception instance minimally (e.g. `new NotFoundException("Claim", Guid.NewGuid())`, `new FluentValidation.ValidationException([new ValidationFailure("LossDate", "Loss date cannot be in the future.")])`). Invoke `new ExceptionHandlingMiddleware().InvokeAsync(context, _ => throw exception)` against a `DefaultHttpContext` with `Response.Body` set to a `MemoryStream`; deserialize the body back to `ErrorResponse` and assert.

- [ ] **Step 2: Run tests, verify they fail (type doesn't exist yet)**

Run: `dotnet test tests/ClaimsModule.UnitTests --filter "FullyQualifiedName~ExceptionHandlingMiddleware"`
Expected: FAIL (compile error — types don't exist)

- [ ] **Step 3: Implement the three exception types in `Application/Common/Exceptions/`**

- [ ] **Step 4: Implement `ErrorResponse` and `ExceptionHandlingMiddleware`**

Standard try/catch-around-`next(context)` middleware; `context.Response.ContentType = "application/json"`; write via `JsonSerializer.Serialize` with camelCase naming policy.

- [ ] **Step 5: Register in `Program.cs`**

`app.UseMiddleware<ExceptionHandlingMiddleware>();` as the very first line after `var app = builder.Build();` (must wrap everything, including auth, once Task 4 adds it).

- [ ] **Step 6: Run tests, verify pass**

Run: `dotnet test tests/ClaimsModule.UnitTests --filter "FullyQualifiedName~ExceptionHandlingMiddleware"`
Expected: PASS

- [ ] **Step 7: Commit**

---

### Task 3: Claim status-transition map and pure condition evaluators

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ClaimStatusTransitions.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/ClaimTransitionConditions.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/ClaimStatusTransitionsTests.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/ClaimTransitionConditionsTests.cs`

**Interfaces:**
- Produces:
  - `ClaimStatusTransitions.GetValidNextStatuses(ClaimStatus from) : IReadOnlyList<ClaimStatus>`
  - `ClaimStatusTransitions.IsValidTransition(ClaimStatus from, ClaimStatus to) : bool`
  - `ClaimTransitionConditions.EvaluateOpenConditions(bool hasActiveClaimant, bool lossDateInFuture, bool lossDescriptionTooShort, bool causeOfLossCodeInactive) : IReadOnlyList<BlockingCondition>` (codes: `BR-C-01`, `BR-C-07`, `BR-C-05` for the critical-issue checks folded in per CC-02/BR-ST-02 overlap, `BR-ST-02`/`BR-P-01`-style message for the claimant check — use code `BR-ST-02` for the claimant-missing case specifically, since BR-ST-02 is the rule that names this condition for the Open transition)
  - `ClaimTransitionConditions.EvaluateClosureConditions(bool anyReserveHistoryPendingApproval, bool lossDateInFuture, bool lossDescriptionTooShort, bool causeOfLossCodeInactive, bool hasActiveClaimant, bool anyReserveComponentNonZero, string? justificationNote) : IReadOnlyList<BlockingCondition>` — codes `CC-01`..`CC-04` respectively; `CC-04` only fires when `anyReserveComponentNonZero && string.IsNullOrWhiteSpace(justificationNote)`.
  - `ClaimTransitionConditions.EvaluateReopenConditions(string? role, string? reason) : IReadOnlyList<BlockingCondition>` — code `BR-ST-04`; fires once for missing/wrong role (`role != "supervisor"`, case-insensitive) and once for an empty `reason`, as two separate entries if both are wrong.
  - `ClaimTransitionConditions.EvaluatePendingPaymentConditions(bool anyReserveApprovedOrAutoApproved) : IReadOnlyList<BlockingCondition>` — code `TRANS-PENDINGPAYMENT-01`.

These are pure functions over booleans — handlers (Task 13) compute the booleans from real repository data and pass them in. This keeps the rule logic directly unit-testable without a database.

- [ ] **Step 1: Write failing tests for `ClaimStatusTransitions`**

One `[Theory]` per `REQUIREMENTS.md` §4.2 row (`Draft→Open` valid, `Draft→Closed` invalid, `Closed→Reopened` valid, `Reopened→Open` valid, `Withdrawn→*` all invalid, etc.) — enumerate the full table from §4.2 as `[InlineData]` rows, both valid and invalid directions.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement `ClaimStatusTransitions`** — a `Dictionary<ClaimStatus, ClaimStatus[]>` literal transcription of §4.2's table (including `Reopened → [Open]` per Resolved Ambiguity 5).

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Write failing tests for `ClaimTransitionConditions`** covering: all-clear (empty list) for each evaluator; each individual boolean flipped produces exactly its named code; CC-04 flips off when `justificationNote` is non-blank; `EvaluateReopenConditions` returns both codes when role is wrong AND reason is empty, one when only one is wrong.

- [ ] **Step 6: Run, verify fail**

- [ ] **Step 7: Implement `ClaimTransitionConditions`**

- [ ] **Step 8: Run, verify pass**

- [ ] **Step 9: Commit**

---

### Task 4: JWT authentication, correlation-id, current-user service, and the first protected endpoint

**Files:**
- Modify: `backend/Directory.Packages.props` (add `Microsoft.AspNetCore.Authentication.JwtBearer`, pin `9.0.x`)
- Modify: `backend/src/ClaimsModule.API/ClaimsModule.API.csproj`
- Create: `backend/src/ClaimsModule.API/Authentication/TestTokenFactory.cs`
- Create: `backend/src/ClaimsModule.API/Authentication/JwtOptions.cs`
- Create: `backend/src/ClaimsModule.API/Authentication/AuthenticationServiceCollectionExtensions.cs`
- Create: `backend/src/ClaimsModule.Application/Common/ICurrentUserService.cs`
- Create: `backend/src/ClaimsModule.API/Authentication/HttpContextCurrentUserService.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/ListClaimStatusesQuery.cs` (query + handler + DTO, one file — this query has no validator and no repository dependency, it's a thin wrapper over Task 3's map)
- Create: `backend/src/ClaimsModule.API/Controllers/ReferenceController.cs`
- Modify: `backend/src/ClaimsModule.API/Program.cs`
- Modify: `backend/src/ClaimsModule.API/appsettings.json`, `appsettings.Development.json`
- Modify: `backend/tests/ClaimsModule.ApiIntegrationTests/ApiFixture.cs` (token minting)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/AuthenticationTests.cs`

**Interfaces:**
- Produces:
  - `JwtOptions { string Issuer; string Audience; string SigningKey; }` bound from config section `"Jwt"`.
  - `TestTokenFactory.CreateToken(string role, Guid userId, JwtOptions options, TimeSpan? lifetime = null) : string` — public static, in the API project (so `ApiIntegrationTests`, which already `ProjectReference`s `ClaimsModule.API`, can call it directly per spec user story 26). Signs with `SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey))`, `SecurityAlgorithms.HmacSha256`. Claims: `ClaimTypes.NameIdentifier` = userId, `ClaimTypes.Role` = role (one of `"handler"`, `"supervisor"`, `"manager"`).
  - `AuthenticationServiceCollectionExtensions.AddJwtAuthentication(this IServiceCollection, IConfiguration)` — binds `JwtOptions`, calls `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with real `ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`/`ValidateIssuerSigningKey` all `true`, using the same `JwtOptions` values.
  - `ICurrentUserService { Guid? UserId { get; } string? Role { get; } }` (Application, both nullable — robust for later non-HTTP callers like Slice D's background jobs, not just this slice's always-`[Authorize]`d endpoints) / `HttpContextCurrentUserService` (API, reads `IHttpContextAccessor`, returns `null`/`null` when there's no authenticated user rather than throwing).
  - `ApiFixture.CreateAuthenticatedClient(string role, Guid? userId = null) : HttpClient` — new helper, builds a client via `CreateClient()` then sets `DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenFactory.CreateToken(role, userId ?? Guid.NewGuid(), <JwtOptions read from the same in-memory config the fixture injected>))`.
- Consumes: Task 3's `ClaimStatusTransitions.GetValidNextStatuses`.

Config values (both `appsettings.json` and `appsettings.Development.json`, plus `ApiFixture`'s `ConfigureAppConfiguration` in-memory overrides so tests don't depend on a file): `Jwt:Issuer` = `"https://claims-module.local"`, `Jwt:Audience` = `"claims-module-api"`, `Jwt:SigningKey` = a ≥32-character dev-only string (document inline that this is a non-secret, local-dev-only value per ADR-0007).

- [ ] **Step 1: Add the NuGet package**

Run: `dotnet add src/ClaimsModule.API package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.22` (match the pinned EF Core line already in use)

- [ ] **Step 2: Write the failing test**

```csharp
[Collection(ApiCollection.Name)]
public class AuthenticationTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Request_without_token_is_rejected()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/reference/claim-statuses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_valid_token_succeeds_and_returns_transition_table()
    {
        using var client = fixture.CreateAuthenticatedClient("handler");
        var response = await client.GetAsync("/api/reference/claim-statuses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ClaimStatusDto>>();
        Assert.Contains(body!, s => s.Status == "Closed" && s.ValidNextStatuses.Contains("Reopened"));
    }
}
```

`ClaimStatusDto { string Status; List<string> ValidNextStatuses; }`.

- [ ] **Step 3: Run, verify fail** (401 route doesn't exist / 404)

- [ ] **Step 4: Implement `JwtOptions`, `TestTokenFactory`, `AuthenticationServiceCollectionExtensions`, `ICurrentUserService`/`HttpContextCurrentUserService`**

- [ ] **Step 5: Implement `ListClaimStatusesQuery`/Handler/DTO and `ReferenceController.GetClaimStatuses()`**

Route: `GET /api/reference/claim-statuses`, `[Authorize]`.

- [ ] **Step 6: Wire `Program.cs`**

In order: `AddJwtAuthentication`, `AddHttpContextAccessor`, register `ICurrentUserService`; add `builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`; register an `ActivityListener` (`ActivitySource.AddActivityListener(new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData })`) before `app.Run()` so `Activity.Current` is populated per request (wayfinder ticket 8 — without a listener, ASP.NET Core's hosting `ActivitySource` won't actually record); add `app.UseAuthentication()` before `app.UseAuthorization()`.

- [ ] **Step 7: Add `CreateAuthenticatedClient` to `ApiFixture`**

- [ ] **Step 8: Run tests, verify pass**

- [ ] **Step 9: Commit**

---

### Task 5: Repository and read-side extensions

**Files:**
- Modify: `backend/src/ClaimsModule.Application/IClaimRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/ClaimRepository.cs`
- Modify: `backend/src/ClaimsModule.Application/IClaimReserveComponentRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/ClaimReserveComponentRepository.cs`
- Create: `backend/src/ClaimsModule.Application/ICauseOfLossCodeRepository.cs`
- Create: `backend/src/ClaimsModule.Persistence/CauseOfLossCodeRepository.cs`
- Create: `backend/src/ClaimsModule.Application/IPolicyRepository.cs`
- Create: `backend/src/ClaimsModule.Persistence/PolicyRepository.cs`
- Create: `backend/src/ClaimsModule.Application/IClaimDocumentRepository.cs`
- Create: `backend/src/ClaimsModule.Persistence/ClaimDocumentRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/PersistenceServiceCollectionExtensions.cs`

No dedicated tests in this task — every method is exercised by the feature tasks that consume it (Tasks 6, 8–13). This is pure plumbing; right-sizing it as its own task keeps later tasks' diffs about behavior, not repository scaffolding.

**Interfaces (produced, all `Task<T>` over `CancellationToken cancellationToken = default`):**

- `IClaimRepository` adds:
  - `GetByIdAsync(Guid claimId) : Claim?` — `Include(c => c.LossEvent).Include(c => c.Parties).Include(c => c.RiskObjects)`, tracked (reused by both read queries and mutating commands per the plan's simplicity-over-microperf call).
  - `ListAsync(ClaimListFilter filter, int page, int pageSize) : (IReadOnlyList<Claim> Items, int TotalCount)` — `ClaimListFilter { ClaimStatus? Status; DateOnly? LossDateFrom; DateOnly? LossDateTo; Guid? AssignedHandlerId; string? CauseOfLossCode; Guid? PolicyId; string? Search; }` (record, defined alongside the interface). `Search` matches `ClaimNumber` or `ClientName` via `Contains`. Date-range filters apply against `LossEvent.LossDate` (requires a join/include).
- `IClaimReserveComponentRepository` adds:
  - `ListByClaimIdAsync(Guid claimId) : IReadOnlyList<ClaimReserveComponent>`
  - `AnyReserveHistoryPendingApprovalAsync(Guid claimId) : bool` (queries `ReserveHistoryEntries` where `ApprovalStatus == PendingApproval`)
  - `AnyReserveHistoryApprovedOrAutoApprovedAsync(Guid claimId) : bool` (queries `ReserveHistoryEntries` where `ApprovalStatus` is `Approved` or `AutoApproved`)
  - `GetTotalReservesByClaimIdsAsync(IEnumerable<Guid> claimIds) : IReadOnlyDictionary<Guid, decimal>` (sum of `CurrentAmount` grouped by `ClaimId`, for `ListClaimsQuery`'s `totalReserves` field)
- `ICauseOfLossCodeRepository`: `GetByCodeAsync(string code) : CauseOfLossCode?`; `ListActiveAsync(PerilCategory? perilCategory) : IReadOnlyList<CauseOfLossCode>` (ordered by `SortOrder`)
- `IPolicyRepository`: `SearchAsync(string query) : IReadOnlyList<Policy>` (matches `PolicyNumber` or `ClientName`, `Contains`, case-insensitive via default SQL Server collation); `GetByIdAsync(Guid policyId) : Policy?`
- `IClaimDocumentRepository`: `ListByClaimIdAsync(Guid claimId) : IReadOnlyList<ClaimDocument>` (returns empty today — forward-compatible for Slice E)

- [ ] **Step 1: Extend `IClaimRepository`/`ClaimRepository` with `GetByIdAsync` and `ListAsync` + `ClaimListFilter`**

- [ ] **Step 2: Extend `IClaimReserveComponentRepository`/`ClaimReserveComponentRepository` with the four reserve-query methods**

- [ ] **Step 3: Create `ICauseOfLossCodeRepository`/`CauseOfLossCodeRepository`, `IPolicyRepository`/`PolicyRepository`, `IClaimDocumentRepository`/`ClaimDocumentRepository`**

- [ ] **Step 4: Register all three new repository interfaces as scoped services in `PersistenceServiceCollectionExtensions.AddPersistence`** (alongside wherever `IClaimRepository`/`IClaimReserveComponentRepository`/`IUnitOfWork` are presumably already registered — check and follow the existing registration pattern there)

- [ ] **Step 5: `dotnet build` the whole solution to confirm no wiring errors**

Run: `dotnet build` (from `backend/`)
Expected: Build succeeded

- [ ] **Step 6: Commit**

---

### Task 6: Remaining reference-data and policy-lookup endpoints

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ListCauseOfLossCodesQuery.cs` (query + handler + DTO)
- Create: `backend/src/ClaimsModule.Application/Policies/SearchPoliciesQuery.cs`
- Create: `backend/src/ClaimsModule.Application/Policies/GetPolicyCoverageQuery.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ReferenceController.cs`
- Create: `backend/src/ClaimsModule.API/Controllers/PoliciesController.cs`
- Create: `backend/src/ClaimsModule.Application/Common/Mapping/ReferenceDataMappingProfile.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ReferenceDataTests.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/PolicyLookupTests.cs`

**Interfaces:**
- Produces: `CauseOfLossCodeDto { string Code; string Name; string PerilCategory; int SortOrder; }`; `PolicySearchResultDto { Guid PolicyId; string PolicyNumber; string ClientName; DateOnly EffectiveDate; DateOnly ExpirationDate; string Status; }`; `PolicyCoverageDto { Guid PolicyId; string PolicyNumber; List<string> CoverageTypes; DateOnly EffectiveDate; DateOnly ExpirationDate; string Status; }` (split `CoverageTypes` on `,`).
- Consumes: Task 5's `ICauseOfLossCodeRepository`, `IPolicyRepository`; Task 2's `NotFoundException`.

Routes: `GET /api/reference/cause-of-loss-codes?perilCategory={category?}`, `GET /api/policies/search?q={query}`, `GET /api/policies/{id}/coverage`. All `[Authorize]`.

- [ ] **Step 1: Write failing tests**

`ReferenceDataTests`: list returns all 10 seeded codes; `?perilCategory=Weather` returns exactly `COL-FLOOD` and `COL-WIND`.
`PolicyLookupTests`: `?q=Meridian` and `?q=POL-2024-001001` both return the same single result; `GET /api/policies/{random-guid}/coverage` → 404 with `type: "NotFound"`; `GET /api/policies/{POL-2025-002001's id}/coverage` → `CoverageTypes == ["Property", "Equipment"]`.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement the three queries/handlers, `ReferenceDataMappingProfile`, and the two controller actions**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Task 7: Audit-column stamping interceptor and audit log service

**Files:**
- Create: `backend/src/ClaimsModule.Persistence/AuditableEntitySaveChangesInterceptor.cs`
- Modify: `backend/src/ClaimsModule.Persistence/PersistenceServiceCollectionExtensions.cs` (register the interceptor on the `DbContext`)
- Create: `backend/src/ClaimsModule.Application/Common/IAuditLogService.cs`
- Create: `backend/src/ClaimsModule.Application/Common/AuditLogEntry.cs`
- Create: `backend/src/ClaimsModule.Persistence/AuditLogService.cs`
- Test: `backend/tests/ClaimsModule.PersistenceTests/AuditableEntitySaveChangesInterceptorTests.cs`

**Why the interceptor is needed (found while re-checking Slice A):** `ClaimsDbContext` has no `SaveChanges` override and no existing interceptor — `OrganisationId`, `CreatedAt`, `UpdatedAt`, `UserCreated`, `UserModified` are **never** populated automatically today. `ClaimTests.cs`/`ClaimChildEntityTests.cs` (Slice A) set every one of these fields by hand on every entity they construct, which is fine for test fixtures but would be a silent-NOT-NULL-violation landmine if every handler from Task 8 onward had to remember to do the same. Fix it once, centrally, here — not per-handler.

**Interfaces:**
- Produces: `AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor` (EF Core's `Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor`), constructed with `ICurrentUserService`. Overrides `SavingChangesAsync`: for every tracked entry whose entity is `AuditableEntity` or `AppendOnlyEntity`:
  - `EntityState.Added`: set `CreatedAt = DateTimeOffset.UtcNow`; set `OrganisationId = dbContext.CurrentOrganisationId` (read via the entry's own `DbContext`, i.e. `eventData.Context!.GetService<...>()`/cast — simplest: give the interceptor a constructor dependency on nothing DbContext-specific and instead read `((ClaimsDbContext)eventData.Context!).CurrentOrganisationId` inside the override); if the entity is `AuditableEntity`, also set `UserCreated = currentUserService.UserId`.
  - `EntityState.Modified` (only applies to `AuditableEntity`, never `AppendOnlyEntity` since those are never updated): set `UpdatedAt = DateTimeOffset.UtcNow`; set `UserModified = currentUserService.UserId`.
  - Register via `services.AddScoped<AuditableEntitySaveChangesInterceptor>()` and `options.AddInterceptors(interceptor)` inside the `AddDbContext` factory in `PersistenceServiceCollectionExtensions.AddPersistence` (resolve it from the service provider passed into the `AddDbContext(( sp, options) => ...)` overload).
  - `IAuditLogService.LogAsync(AuditLogEntry entry, CancellationToken)`. `AuditLogEntry { Guid ClaimId; string EventType; string Description; string? OldValue; string? NewValue; Guid? RelatedEntityId; string? RelatedEntityType; }` (record). `IAuditLogService.GetForClaimAsync(Guid claimId, int page, int pageSize, CancellationToken) : (IReadOnlyList<ClaimAuditLog> Items, int TotalCount)` ordered `CreatedAt DESC`.

`AuditLogService` is constructed with the same scoped `ClaimsDbContext` used by the repositories in that request. `LogAsync` only calls `dbContext.ClaimAuditLogEntries.Add(...)` — **it does not call `SaveChangesAsync` itself**, and it does **not** set `OrganisationId`/`CreatedAt` itself either, since the interceptor now does that for every `AppendOnlyEntity` on `Added`. It still sets `CorrelationId` directly (`Guid.Parse(Activity.Current?.TraceId.ToHexString() ?? Guid.NewGuid().ToString("N"))`) since that's not a generic audit-column concern the interceptor should own. This is what makes the claim-create-plus-audit insert in Task 8 a single atomic `SaveChangesAsync`.

With this in place, **no handler in this plan (Task 8 onward) ever sets `OrganisationId`, `CreatedAt`, `UpdatedAt`, `UserCreated`, or `UserModified` directly** — only the business-meaningful fields.

- [ ] **Step 1: Write a failing `PersistenceTests` test** (this is the one piece of real I/O-dependent logic in this task, hence the one exception to "no dedicated test" elsewhere in this plan): build a `ClaimsDbContext` with the interceptor wired exactly as `AddPersistence` will wire it, insert a bare `new Claim { ClaimId = ..., ClaimNumber = ..., Status = ..., ReportedDate = ... }` (deliberately leaving `OrganisationId`/`CreatedAt` unset), call `SaveChangesAsync`, re-read the row, assert `OrganisationId == SeedData.OrganisationId` and `CreatedAt` is within the last minute.

- [ ] **Step 2: Run, verify fail**

Run: `dotnet test tests/ClaimsModule.PersistenceTests --filter "FullyQualifiedName~AuditableEntitySaveChangesInterceptor"` (requires Docker)
Expected: FAIL

- [ ] **Step 3: Implement `AuditableEntitySaveChangesInterceptor`; wire it into `PersistenceServiceCollectionExtensions.AddPersistence`**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Implement `AuditLogEntry`, `IAuditLogService`, `AuditLogService`; register `IAuditLogService` as scoped in `PersistenceServiceCollectionExtensions`** (no dedicated test for this half — proven by Task 8's `ClaimAuditLog` row assertions and Task 11's audit-read-endpoint tests)

- [ ] **Step 6: `dotnet build`**

- [ ] **Step 7: Commit**

---

### Task 8: `CreateClaimCommand` (FNOL intake)

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/CreateClaim/CreateClaimCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/CreateClaim/CreateClaimCommandValidator.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/CreateClaim/CreateClaimCommandHandler.cs`
- Create: `backend/src/ClaimsModule.Application/Common/Behaviors/ValidationBehavior.cs`
- Create: `backend/src/ClaimsModule.Application/Common/IHasValidationWarnings.cs`
- Create: `backend/src/ClaimsModule.Application/Common/Mapping/ClaimMappingProfile.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ClaimDto.cs`, `LossEventDto.cs`, `ClaimPartyDto.cs`, `ClaimRiskObjectDto.cs`
- Create: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Modify: `backend/src/ClaimsModule.Application/ApplicationServiceCollectionExtensions.cs` (register `ValidationBehavior` as a MediatR pipeline behavior)
- Test: `backend/tests/ClaimsModule.UnitTests/ValidationBehaviorTests.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ClaimCreationTests.cs`

**Interfaces:**
- Produces:
  - `IHasValidationWarnings { List<ValidationFailure> Warnings { get; } }` (`FluentValidation.Results.ValidationFailure`)
  - `ValidationBehavior<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse>` — resolves `IEnumerable<IValidator<TRequest>>`, runs all, splits failures by `.Severity`; if any `Severity.Error` failure exists, throws `new FluentValidation.ValidationException(errorFailures)` (only the Error-severity ones); else if `TRequest : IHasValidationWarnings` and there are Warning-severity failures, assigns them to `request.Warnings`; always calls `next()` when not throwing.
  - `CreateClaimCommand : IRequest<ClaimDto>, IHasValidationWarnings { Guid? PolicyId; DateTimeOffset LossDate; string LossDescription; string? LossLocation; string CauseOfLossCode; decimal? EstimatedLossAmount; List<CreatePartyDto> Parties; List<CreateRiskObjectDto> RiskObjects; List<ValidationFailure> Warnings { get; } = []; }` — `CreatePartyDto { PartyRole PartyRole; PartyType PartyType; string? FirstName; string? LastName; string? CompanyName; string? Email; string? Phone; }`, `CreateRiskObjectDto { AssetType AssetType; string AssetDescription; string? DamageDescription; bool IsPrimary; string? AssetReference; }` (both nested in `CreateClaimCommand.cs`).
  - `ClaimDto { Guid ClaimId; string ClaimNumber; string Status; Guid? PolicyId; string? PolicyNumber; string? ClientName; DateTimeOffset ReportedDate; LossEventDto LossEvent; List<ClaimPartyDto> Parties; List<ClaimRiskObjectDto> RiskObjects; }`
- Consumes: Task 5's `IClaimRepository`, `ICauseOfLossCodeRepository`, `IPolicyRepository`; Task 7's `IAuditLogService`; Task 4's `ICurrentUserService`; Task 2's exceptions.

**Validator rules (exact §8 messages, severities per Resolved Ambiguity 1):**

| Property | Rule | Severity | Message |
|---|---|---|---|
| `LossDate` | not default | Error | `"Loss date is required."` |
| `LossDate` | `<= DateTimeOffset.UtcNow` | Error | `"Loss date cannot be in the future."` |
| `LossDescription` | `NotEmpty().MinimumLength(20)` | Error | `"Loss description is required and must be at least 20 characters."` |
| `CauseOfLossCode` | `MustAsync` exists-and-active via `ICauseOfLossCodeRepository.GetByCodeAsync` | Error | `"Cause of loss code is not recognised or is inactive."` |
| `Parties` | `Must(p => p.Any(x => x.PartyRole == PartyRole.Claimant))` | Error | `"At least one Claimant party is required to open a claim."` |
| `PolicyId` | `Null` | Warning | `"No policy linked. Policy must be associated before reserves can be set."` |
| `PolicyId` (when not null) | `MustAsync` policy exists via `IPolicyRepository.GetByIdAsync` | Warning | `"Linked policy could not be found."` |
| (cross-field, `RuleFor(x => x)`) | when `PolicyId` resolves to a real policy: `LossDate` within `[EffectiveDate, ExpirationDate]` | Warning | `"Loss date is outside the policy effective period."` |
| `RiskObjects` | `Must(r => r.Count > 0)` | Warning | `"No risk objects linked to this claim."` |

Nested nullable-field shape checks (basic, Error, no `BR-*` id — e.g. `RuleForEach(x => x.Parties)` → `RuleFor(p => p.FirstName).NotEmpty().When(p => p.PartyType == PartyType.Person)`, similarly `CompanyName` for `Company`; `RuleForEach(x => x.RiskObjects)` → `AssetDescription` `NotEmpty`).

**Handler steps** (see Resolved Ambiguity 4 for the `ClaimId` assignment rationale):

1. `var claimNumber = await claimNumberGenerator.GenerateAsync(ct);`
2. `Policy? policy = command.PolicyId is { } pid ? await policyRepository.GetByIdAsync(pid, ct) : null;`
3. Build `claim = new Claim { ClaimId = Guid.NewGuid(), ClaimNumber = claimNumber, PolicyId = command.PolicyId, PolicyNumber = policy?.PolicyNumber, ClientName = policy?.ClientName, Status = ClaimStatus.Draft, ReportedDate = DateTimeOffset.UtcNow, AssignedHandlerId = currentUserService.UserId };` — **do not set `OrganisationId`/`CreatedAt` here**; Task 7's `AuditableEntitySaveChangesInterceptor` stamps both automatically on insert.
4. Populate `claim.LossEvent`, `claim.Parties`, `claim.RiskObjects` navigation collections from the command (per `Claim.cs`'s own doc comment on why these are navigation-populated, not scalar-FK-populated) — same rule applies, don't set their `OrganisationId`/`CreatedAt` either.
5. `claimRepository.Add(claim);`
6. `await auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "CLAIM_CREATED", $"Claim {claim.ClaimNumber} created.", null, null, null, null), ct);`
7. `foreach (var warning in command.Warnings) await auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "VALIDATION_ISSUE_ADDED", warning.ErrorMessage, null, null, null, null), ct);`
8. `await claimRepository.CommitChangesAsync(ct);` — single `SaveChangesAsync`, which is also the one call that triggers the interceptor and actually persists everything atomically.
9. Map `claim` → `ClaimDto` via AutoMapper, return.

`ClaimsController.CreateClaim([FromBody] CreateClaimCommand command)`: `[HttpPost] [Authorize]`, returns `CreatedAtAction(nameof(GetClaimDetail), new { id = result.ClaimId }, result)` — note `GetClaimDetail` doesn't exist until Task 10; stub the `CreatedAtAction` route name now and verify it once Task 10 lands (or return a plain `Created($"/api/claims/{result.ClaimId}", result)` now to avoid a forward reference, swapping to `CreatedAtAction` in Task 10 once the action exists — simpler, do this).

- [ ] **Step 1: Write failing `ValidationBehaviorTests`** (pure, mocked `IValidator<TRequest>`): no validators registered → `next()` called, no throw; all-Error failures → throws `ValidationException` containing only those failures, `next()` never called; mixed Error+Warning → throws with only the Error ones; all-Warning on a request implementing `IHasValidationWarnings` → does not throw, `next()` called, `request.Warnings` populated; all-Warning on a request NOT implementing the interface → does not throw (warnings silently dropped, which is fine — nothing today has warnings without implementing the interface).

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement `IHasValidationWarnings`, `ValidationBehavior`; register in `ApplicationServiceCollectionExtensions`** via `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));`

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Write failing `ClaimCreationTests`** (derive `ApiTestBase`), one `[Fact]` per row:
  - Happy path: 201, `ClaimNumber` matches `^CLM-\d{4}-\d{7}$`, `Status == "Draft"`.
  - BR-C-01: `LossDate` = tomorrow → 422, `errors["LossDate"]` contains the exact message.
  - BR-C-02 boundary: policy `POL-2023-000099` (expired 2021-12-31) with `LossDate` = 2026-01-01 → 201 created (Warning doesn't block), and a later `GET .../audit` (once Task 11 exists — defer this specific assertion there, or just assert 201 here and leave the audit-row check to Task 11) — for now assert 201 + `Status == "Draft"`.
  - BR-C-03: `Parties = []` → 422, `errors["Parties"]`.
  - BR-C-05 (three-way, Review Focus #4): nonexistent code `"COL-NOPE"` → 422; an explicitly-seeded-inactive code (none exist in seed data — add one case: since all 10 seeded codes are `IsActive = true`, this sub-case needs a code that's inactive. Either seed one more inactive code in Task 5/6 or — simpler — don't seed a new one; instead directly assert only the "nonexistent" half of the boundary here, and rely on `ListCauseOfLossCodesQuery`'s own correctness (Task 6) plus the validator's `MustAsync` logic checking `.IsActive` (unit-provable by inspecting the predicate) for the inactive half. Note this honestly as a minor coverage gap rather than fabricating seed data the spec didn't ask for.); valid active code `"COL-FIRE"` → 201.
  - BR-C-06: `PolicyId = null` → 201, `PolicyId == null` in response.
  - BR-C-07 boundary: `LossDescription` of exactly 20 chars → 201; 19 chars → 422.
  - Concurrency (Review Focus #1): fire 10 parallel `POST /api/claims` with otherwise-identical valid bodies; assert all 10 responses are 201 and all 10 `ClaimNumber`s are distinct.

- [ ] **Step 6: Run, verify fail**

- [ ] **Step 7: Implement `CreateClaimCommand`, `CreateClaimCommandValidator`, `CreateClaimCommandHandler`, `ClaimMappingProfile`, DTOs, and `ClaimsController.CreateClaim`**

- [ ] **Step 8: Run, verify pass**

- [ ] **Step 9: Commit**

---

### Task 9: `ValidateClaimQuery`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ValidateClaimQuery.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ValidationIssueDto.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ClaimValidationEndpointTests.cs`

**Interfaces:**
- Produces: `ValidateClaimQuery : IRequest<List<ValidationIssueDto>>` — identical shape to `CreateClaimCommand` minus `IHasValidationWarnings` (it's never thrown at, so no warnings-carrying need); `ValidationIssueDto { string PropertyName; string Message; string Severity; }` (`"Error"` or `"Warning"`, from `ValidationFailure.Severity.ToString()`).
- Consumes: `CreateClaimCommandValidator` directly (constructed via DI, `IValidator<CreateClaimCommand>`, injected into the handler) — the handler builds a transient `CreateClaimCommand` from the query's identical fields and calls `validator.ValidateAsync(transientCommand, ct)` manually, bypassing `ValidationBehavior` entirely (no `IValidator<ValidateClaimQuery>` is ever registered, so the pipeline behavior is a no-op for this request type). Returns **every** failure (both severities), never throws.

Route: `POST /api/claims/validate`, `[Authorize]`, always `200`.

- [ ] **Step 1: Write failing tests**: a body with a future `LossDate` AND an empty `Parties` list AND `PolicyId = null` → 200, response contains exactly 3 issues with the right `PropertyName`/`Severity` combination (`LossDate`/`Error`, `Parties`/`Error`, `PolicyId`/`Warning`); a fully-valid body → 200, empty list; nothing is ever persisted (assert no new `Claims` row — query count before/after, or simpler: assert `GET /api/claims` count is unchanged, once Task 10 exists; for now, just assert the response and move on, this is implicitly covered by the fact the query never calls any repository `Add`/`CommitChangesAsync`).

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement `ValidateClaimQuery`/Handler/DTO and `ClaimsController.ValidateClaim`**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Task 10: `GetClaimDetailQuery` and `ListClaimsQuery`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/GetClaimDetailQuery.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/ListClaimsQuery.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ClaimDetailDto.cs`, `ClaimSummaryDto.cs`, `ReserveSummaryDto.cs`, `ClaimDocumentDto.cs`, `PagedResult.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ClaimReadTests.cs`

**Interfaces:**
- Produces: `PagedResult<T> { List<T> Items; int TotalCount; int Page; int PageSize; }` (generic, reusable by Task 11 too); `ClaimSummaryDto { Guid ClaimId; string ClaimNumber; string? ClientName; string? PolicyNumber; DateTimeOffset LossDate; string Status; decimal TotalReserves; }`; `ClaimDetailDto` = everything in `ClaimDto` (Task 8) plus `List<ReserveSummaryDto> Reserves; List<ClaimDocumentDto> Documents; List<AuditLogEntryDto> RecentAuditEntries;` (`AuditLogEntryDto { DateTimeOffset CreatedAt; string EventType; string Description; }` — define alongside, reused by Task 11); `ReserveSummaryDto { Guid ReserveComponentId; string Component; decimal CurrentAmount; string Status; }`; `ClaimDocumentDto { Guid ClaimDocumentId; string DocumentName; string DocumentType; DateTimeOffset UploadedAt; }`.
- Consumes: Task 5's `IClaimRepository.GetByIdAsync`/`ListAsync`, `IClaimReserveComponentRepository.ListByClaimIdAsync`/`GetTotalReservesByClaimIdsAsync`, `IClaimDocumentRepository.ListByClaimIdAsync`; Task 7's `IAuditLogService.GetForClaimAsync` (call with `page=1, pageSize=5` for the "recent" slice); Task 2's `NotFoundException`.

Routes: `GET /api/claims?status=&dateFrom=&dateTo=&assignedHandlerId=&causeOfLossCode=&policyId=&search=&page=1&pageSize=20`, `GET /api/claims/{id}`. Both `[Authorize]`.

`ListClaimsQueryHandler`: calls `claimRepository.ListAsync(filter, page, pageSize, ct)`, then `reserveComponentRepository.GetTotalReservesByClaimIdsAsync(items.Select(c => c.ClaimId), ct)` once for the whole page (avoids N+1), maps into `ClaimSummaryDto`s pulling each claim's total from that dictionary (default `0m` if absent).

`GetClaimDetailQueryHandler`: `claimRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException("Claim", id)`, then the three parallel lookups above, assembled into `ClaimDetailDto`.

- [ ] **Step 1: Write failing tests**: create 2–3 claims via Task 8's endpoint with varying `CauseOfLossCode`/dates/search terms; `GET /api/claims?status=Draft` returns all of them; `GET /api/claims?search=<part of one's ClientName>` returns only the match; a claim with no reserves shows `TotalReserves == 0` (Review Focus #5, half); pagination (`page=1&pageSize=1`) returns `TotalCount` = full count but `Items.Count == 1`; `GET /api/claims/{id}` for a real id returns full detail incl. empty `Reserves`/`Documents` lists and at least the `CLAIM_CREATED` entry in `RecentAuditEntries`; `GET /api/claims/{random-guid}` → 404.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement everything above; also swap Task 8's `ClaimsController.CreateClaim`'s `Created(...)` call to `CreatedAtAction(nameof(GetClaimDetail), ...)` now that the action exists**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Task 11: `GetClaimAuditLogQuery`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/GetClaimAuditLogQuery.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ClaimAuditLogTests.cs`

**Interfaces:**
- Produces: `GetClaimAuditLogQuery : IRequest<PagedResult<AuditLogEntryDto>>`.
- Consumes: Task 7's `IAuditLogService.GetForClaimAsync`; Task 2's `NotFoundException` (verify the claim exists first via `IClaimRepository.GetByIdAsync`, else 404 rather than an empty list for a bad id).

Route: `GET /api/claims/{id}/audit?page=1&pageSize=20`, `[Authorize]`.

- [ ] **Step 1: Write failing tests**: create a claim, `GET .../audit` returns at least one `CLAIM_CREATED` entry, newest-first; create a claim whose body triggers a BR-C-02/BR-C-06 warning, assert a `VALIDATION_ISSUE_ADDED` entry is present (this is the deferred assertion from Task 8's BR-C-02/06 cases — pick it up here); `GET /api/claims/{random-guid}/audit` → 404.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Task 12: Party management (`AddClaimPartyCommand`, `RemoveClaimPartyCommand`)

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/AddClaimParty/AddClaimPartyCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/AddClaimParty/AddClaimPartyCommandValidator.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/AddClaimParty/AddClaimPartyCommandHandler.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/RemoveClaimParty/RemoveClaimPartyCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/RemoveClaimParty/RemoveClaimPartyCommandHandler.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/PartyManagementTests.cs`

**Interfaces:**
- Produces: `AddClaimPartyCommand : IRequest<ClaimPartyDto> { Guid ClaimId; PartyRole PartyRole; PartyType PartyType; string? FirstName; string? LastName; string? CompanyName; string? Email; string? Phone; }`; `RemoveClaimPartyCommand : IRequest { Guid ClaimId; Guid PartyId; }` (MediatR `IRequest` with no response — handler returns `Unit`/nothing meaningful, controller returns `204`).
- Consumes: Task 5's `IClaimRepository.GetByIdAsync` (already includes `Parties`); Task 2's `NotFoundException`, `BusinessRuleViolationException`/`BlockingCondition`; Task 7's `IAuditLogService`.

**`AddClaimPartyCommandHandler`:** load claim via `GetByIdAsync`, 404 if missing; append a new `ClaimParty` to `claim.Parties` (reuse the same Person/Company shape validation as Task 8's nested party validator — same `AddClaimPartyCommandValidator` rules: `FirstName`/`LastName` required when `Person`, `CompanyName` required when `Company`, both Error); audit `PARTY_ADDED`; `CommitChangesAsync`.

**`RemoveClaimPartyCommandHandler`:** load claim; find the target party by id among `claim.Parties`, 404 if missing; if `party.PartyRole == Claimant` and it's the last **active** `Claimant` on the claim (`claim.Parties.Count(p => p.IsActive && p.PartyRole == Claimant) == 1` and this is it) → throw `BusinessRuleViolationException([new BlockingCondition("BR-P-01", "A claim must retain at least one active Claimant party.")])`; else `party.IsActive = false`; audit `PARTY_REMOVED`; `CommitChangesAsync`.

Routes: `POST /api/claims/{id}/parties` (201, body = `ClaimPartyDto`), `DELETE /api/claims/{id}/parties/{partyId}` (204). Both `[Authorize]`.

- [ ] **Step 1: Write failing tests**: add a party to an existing claim → 201, claim now has 2 parties via `GET /api/claims/{id}`; remove a non-last Claimant (Review Focus #2: create a claim with 2 Claimants, remove one) → 204, the other remains active; remove the last active Claimant → 422 with `blockingConditions[0].code == "BR-P-01"`; add/remove against a nonexistent claim → 404; add/remove a nonexistent party id → 404.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Task 13: `TransitionClaimStatusCommand`

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/TransitionClaimStatus/TransitionClaimStatusCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/TransitionClaimStatus/TransitionClaimStatusCommandHandler.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs`
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/StatusTransitionTests.cs`

**Interfaces:**
- Produces: `TransitionClaimStatusCommand : IRequest<ClaimDto> { Guid ClaimId; ClaimStatus TargetStatus; string? Reason; string? JustificationNote; }`.
- Consumes: Task 3's `ClaimStatusTransitions`, `ClaimTransitionConditions`; Task 5's `IClaimRepository.GetByIdAsync`, `IClaimReserveComponentRepository` (all four query methods), `ICauseOfLossCodeRepository.GetByCodeAsync` (for the CC-02 re-check); Task 4's `ICurrentUserService`; Task 2's exceptions; Task 7's `IAuditLogService`.

**Handler logic:**

1. Load `claim` via `GetByIdAsync`, 404 if missing.
2. `if (!ClaimStatusTransitions.IsValidTransition(claim.Status, command.TargetStatus)) throw new BusinessRuleViolationException([new BlockingCondition("BR-ST-01", $"Transition from {claim.Status} to {command.TargetStatus} is not permitted. Valid next statuses: {string.Join(", ", ClaimStatusTransitions.GetValidNextStatuses(claim.Status))}.")]);`
3. Switch on `command.TargetStatus`:
   - `Open`: compute booleans (`hasActiveClaimant` from `claim.Parties`; `lossDateInFuture`/`lossDescriptionTooShort` from `claim.LossEvent`; `causeOfLossCodeInactive` via `ICauseOfLossCodeRepository.GetByCodeAsync(claim.LossEvent.CauseOfLossCode)`), call `ClaimTransitionConditions.EvaluateOpenConditions(...)`; if non-empty, throw `BusinessRuleViolationException(conditions)`.
   - `PendingPayment`: `anyReserveApprovedOrAutoApproved` via `AnyReserveHistoryApprovedOrAutoApprovedAsync(claim.ClaimId)`; `EvaluatePendingPaymentConditions`; throw if non-empty.
   - `Closed`: gather all five booleans (`AnyReserveHistoryPendingApprovalAsync`, the three LossEvent-derived booleans + `causeOfLossCodeInactive` again, `hasActiveClaimant`, `anyReserveComponentNonZero` from `ListByClaimIdAsync(...).Any(c => c.CurrentAmount != 0)`, `command.JustificationNote`); `EvaluateClosureConditions`; throw if non-empty; else set `claim.ClosedAt = DateTimeOffset.UtcNow; claim.ClosureReason = command.Reason;` and audit `CLAIM_CLOSED` (NewValue = closure reason) in addition to the generic `STATUS_CHANGED` entry below.
   - `Reopened`: `EvaluateReopenConditions(currentUserService.Role, command.Reason)`; throw if non-empty; else perform the two-step cascade from Resolved Ambiguity 5 — write `STATUS_CHANGED` (old=`Closed`, new=`Reopened`), write `CLAIM_REOPENED` (NewValue = reason), set `claim.Status = ClaimStatus.Open`, write a second `STATUS_CHANGED` (old=`Reopened`, new=`Open`); `return` early after committing (skip the generic step 4 below, since both audit writes already happened).
   - everything else (`UnderInvestigation`, `Withdrawn`, the plain `Open`-already-handled-above, etc.): no extra condition beyond step 2's map check; `Withdrawn` additionally requires `!string.IsNullOrWhiteSpace(command.Reason)` — `Must`-style inline check in the handler throwing `BusinessRuleViolationException([new BlockingCondition("BR-ST-01", "A withdrawal reason is required.")])` (reuse `BR-ST-01`'s code since this is still "the transition's condition wasn't met," no dedicated id exists for it).
4. (generic path, all targets except `Reopened` which already returned) `var oldStatus = claim.Status; claim.Status = command.TargetStatus;` audit `STATUS_CHANGED` (`OldValue = oldStatus`, `NewValue = command.TargetStatus`).
5. `await claimRepository.CommitChangesAsync(ct);`
6. Map and return `ClaimDto`.

Route: `PUT /api/claims/{id}/status`, body `{ targetStatus, reason?, justificationNote? }`, `[Authorize]`.

- [ ] **Step 1: Write failing tests** — one per Review Focus item plus the full BR-ST/CC matrix:
  - `Draft→Open` happy path (claim has a Claimant from creation) → 200, `Status == "Open"`.
  - Invalid transition `Draft→Closed` → 422, `blockingConditions[0].code == "BR-ST-01"`, message lists `Open` as the only valid next status.
  - `Open→Closed` with a `PendingApproval` `ReserveHistory` row present (insert directly via a raw SQL helper against `fixture.ClaimsConnectionString`, since no Slice B endpoint creates reserves) → 422, contains `CC-01`.
  - `Open→Closed` with no reserves at all and a valid claim → 200 `Closed`.
  - CC-04 (Review Focus #3): insert a `ClaimReserveComponent` with `CurrentAmount = 500` directly via SQL; `Open→Closed` with no `justificationNote` → 422 contains `CC-04`; same request with `justificationNote: "approved by ops"` → 200.
  - `Open→PendingPayment` with no approved `ReserveHistory` → 422 `TRANS-PENDINGPAYMENT-01`; with one `AutoApproved` row inserted via SQL → 200.
  - `Closed→Reopened` as `handler` role (Review Focus #5) → 422 `BR-ST-04`; as `supervisor` with empty `reason` → 422 `BR-ST-04`; as `supervisor` with a reason → 200, final `Status == "Open"` (not `"Reopened"`), and `GET .../audit` shows `STATUS_CHANGED`(Closed→Reopened), `CLAIM_REOPENED`, `STATUS_CHANGED`(Reopened→Open) in order.
  - `Open→Withdrawn` with empty reason → 422; with a reason → 200.

- [ ] **Step 2: Run, verify fail**

- [ ] **Step 3: Implement**

- [ ] **Step 4: Run, verify pass**

- [ ] **Step 5: Commit**

---

### Final steps (not a plan task — part of the `/implement` workflow wrapping this plan)

- [ ] Run `dotnet build` and `dotnet test tests/ClaimsModule.UnitTests` after every task (fast loop).
- [ ] Run the full `dotnet test` (Docker required) once after Task 13.
- [ ] Invoke the `code-review` skill against the whole branch.
- [ ] Commit (already happening per-task above) — no separate final commit needed unless code review produces fixups.
