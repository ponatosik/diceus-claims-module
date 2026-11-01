# Slice E: Document Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a claims handler upload supporting documents to a claim (multipart upload, Azure Blob primary / local filesystem fallback) and list a claim's documents with short-lived download URLs, behind a provider-abstracted `IStorageService`.

**Architecture:** `IStorageService` lives in `ClaimsModule.Application`; `AzureBlobStorageService` and `LocalFileSystemStorageService` live in `ClaimsModule.Infrastructure` and are selected per-request via a lazily-evaluated DI factory (not an eager read inside `AddInfrastructure`, per the config-timing bug found and fixed in Slice D). Filename sanitisation happens once, in the upload command handler, before either implementation ever sees a filename — neither implementation duplicates it.

**Tech Stack:** `Azure.Storage.Blobs` (new), ASP.NET Core static file middleware for the local fallback, existing MediatR/EF Core/FluentValidation stack.

**Spec:** `specs/005-document-management.md`; `REQUIREMENTS.md` §7.4 (BR-D-01..03), §9.7 (ClaimDocuments), §13 (Document Management), §10.1 (document endpoints), §14.1 (DOCUMENT_UPLOADED).

## Global Constraints

- Blob container: `claim-documents`. Storage path (Azure case, BR-D-01): `claim-documents/{organisationId}/{claimId}/{sanitisedFilename}`.
- Local fallback root (§13's Fallback row): `/uploads/{organisationId}/{claimId}/`.
- SAS URL TTL: exactly 1 hour (BR-D-02). Document bytes are never proxied through the API for the Azure case.
- MIME-type allowlist (§13): `application/pdf`, `image/jpeg`, `image/png`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` (DOCX), `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (XLSX), `text/plain`, `text/csv`.
- Max file size: 50 MB (§13 — no hard limit in the source spec, 50MB is this project's own reasonable choice).
- Provider selection config key (spec's own literal name): `StorageProvider` = `AzureBlob` | `LocalFileSystem`.
- `DOCUMENT_UPLOADED` audit entry on every successful upload, `RelatedEntityId` = the new `ClaimDocument.ClaimDocumentId` (§13/§14.1).
- No document deletion, no content/virus inspection beyond MIME-type allowlisting, no frontend upload-progress UI (explicitly Out of Scope).

## Resolved Ambiguities

1. **Filename sanitisation happens once, in `UploadClaimDocumentCommandHandler`**, before calling `IStorageService.UploadAsync` — neither `AzureBlobStorageService` nor `LocalFileSystemStorageService` re-sanitises. `IStorageService.UploadAsync` takes the already-sanitised filename as a plain string, plus the organisation/claim ids, and each implementation assembles its own storage-specific path from those three values.
2. **`ClaimDocument.BlobPath` stores whatever path/identifier each provider's `UploadAsync` call returns** — not one universal scheme. `AzureBlobStorageService` returns `"claim-documents/{organisationId}/{claimId}/{fileName}"` (container + blob name, descriptive); `LocalFileSystemStorageService` returns `"/uploads/{organisationId}/{claimId}/{fileName}"` (its own root, matching §13's Fallback row literally). Both can resolve `GetDownloadUrlAsync(blobPath)` later from exactly the string they returned.
3. **`IStorageService` provider selection is resolved lazily, per-request** — `services.AddScoped<IStorageService>(sp => ...)` reads `IConfiguration` inside the factory delegate (invoked at first resolution, well after `WebApplicationFactory`'s test config merge completes), not inside `AddInfrastructure`'s own eager method body. This is the exact pattern Slice D's review fix pass established for `Hangfire:StartServer` after finding the eager-read version silently used the wrong value under tests — same risk, same fix, applied proactively this time.
4. **The local fallback's "short-lived URL" is ASP.NET Core static-file middleware** mapped at request path `/uploads` over the configured local root, wired in `Program.cs` *after* `var app = builder.Build()` (using `app.Configuration`, the final merged configuration — not `builder.Configuration`, which is too early per the same Slice D lesson). It is genuinely unauthenticated and not time-limited. REQUIREMENTS.md's 1-hour-TTL SAS requirement (BR-D-02) is scoped to the Azure case; the spec's own Implementation Decisions explicitly leave the local scheme to implementation discretion.
5. **`LocalFileSystemStorageService` gets a direct, non-HTTP instantiation test** (in `ClaimsModule.UnitTests`, against a real temp directory, cleaned up after) — not a second `ApiFixture`/`WebApplicationFactory` host. `ApiFixture` is one shared host per test collection; switching `StorageProvider` mid-run isn't practical, and a second full host is disproportionate for proving one class's file I/O. This test does real filesystem I/O despite living in the "zero I/O" UnitTests project — a deliberate, narrow exception, since it's the only way to exercise this class without a second host.
6. **MIME-type allowlist is checked against the client-declared `Content-Type`**, not file content/magic bytes — REQUIREMENTS.md explicitly scopes out content inspection. `UploadClaimDocumentCommandValidator`'s rule is a field-level FluentValidation rule producing the standard `errors` shape (Slice B's validation pipeline), not `blockingConditions`.
7. **Kestrel's default `MaxRequestBodySize` (~28.6MB) and the multipart form parser's own body-length limit sit below this slice's 50MB allowlist ceiling** — without raising them, a 30-45MB upload would be rejected by the server pipeline before ever reaching `UploadClaimDocumentCommandValidator`, as a bare connection failure rather than the intended validation error. `[RequestSizeLimit]`/`[RequestFormLimits]` on the controller action raise both to 50MB so the validator is what actually enforces the limit.

## Review Focus

- A filename containing `../` or an absolute path (e.g. `../../etc/passwd`, `C:\Windows\x.txt`) must be sanitised to something that can't escape the intended `{organisationId}/{claimId}/` prefix — BR-D-01 names this explicitly, and it's the one input a reasonable person would expect this system to defend against even without being told.
- Uploading to a claim id that doesn't exist must 404, not silently create an orphaned blob with no `ClaimDocuments` row pointing at it.
- Listing documents for a claim that has none must return an empty list (200), not an error.
- A SAS URL (or local URL) returned by the list endpoint must actually be fetchable and return the exact bytes that were uploaded — not just a well-formed-looking string. Proving the URL round-trips, not just that it was generated, is the only way to know BR-D-02 is really satisfied.
- A disguised upload (a `.exe` renamed to claim a `text/plain` Content-Type) is accepted, because REQUIREMENTS.md explicitly scopes out content inspection — worth a test proving the allowlist checks the declared header, so nobody later "fixes" this as if it were a bug.

---

### Task 1: `IStorageService`, filename sanitiser, and the document DTO

**Files:**
- Create: `backend/src/ClaimsModule.Application/IStorageService.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/DocumentFileNameSanitizer.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/Dtos/ClaimDocumentDto.cs`
- Test: `backend/tests/ClaimsModule.UnitTests/DocumentFileNameSanitizerTests.cs` (new)

**Interfaces:**
- Produces: `IStorageService.UploadAsync(Guid organisationId, Guid claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken = default) : Task<string>` (returns the provider-specific `BlobPath` to persist) and `IStorageService.GetDownloadUrlAsync(string blobPath, CancellationToken = default) : Task<string>`. Consumed by Tasks 3 and 4 (implementations) and Tasks 5/6 (handlers). `DocumentFileNameSanitizer.Sanitize(string fileName) : string`. Consumed by Task 5's handler. `ClaimDocumentDto(Guid ClaimDocumentId, Guid ClaimId, string DocumentType, string DocumentName, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt, Guid? UploadedByUserId, string? Notes, string DownloadUrl)`. Consumed by Tasks 5 and 6.

- [ ] **Step 1: Write the failing tests** in `DocumentFileNameSanitizerTests`:
  - `Sanitize_strips_path_traversal_and_directory_components`: `Sanitize("../../etc/passwd")` and `Sanitize("..\\..\\windows\\x.txt")` both return a bare filename with no `/`, `\`, or `..` segments (e.g. `"passwd"`/`"x.txt"`) — proves `Path.GetFileName`-style stripping, pinning Review Focus item 1.
  - `Sanitize_replaces_unsafe_characters_but_keeps_a_normal_name_unchanged`: `Sanitize("report (final).pdf")` → `"report_(final).pdf"` or similar (document whatever exact replacement rule you pick — the test is what pins it, not this line); `Sanitize("invoice-2026.pdf")` is unchanged (already safe).
  - `Sanitize_never_returns_empty`: `Sanitize("../../")` (nothing left after stripping directories) returns a non-empty fallback name, not `""`.
- [ ] **Step 2: Run, verify fail** (class doesn't exist).
- [ ] **Step 3: Implement.** `IStorageService` as an interface with the two methods above. `DocumentFileNameSanitizer.Sanitize`: take `Path.GetFileName(fileName)` first (defeats traversal), then replace every character that isn't a letter, digit, `.`, `-`, or `_` with `_`; if the result is empty, return a fixed fallback (e.g. `"file"`). `ClaimDocumentDto` as a plain record.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.UnitTests --filter "FullyQualifiedName~DocumentFileNameSanitizer"`
- [ ] **Step 5: Commit**

---

### Task 2: `IClaimDocumentRepository` write support

**Files:**
- Modify: `backend/src/ClaimsModule.Application/IClaimDocumentRepository.cs`
- Modify: `backend/src/ClaimsModule.Persistence/ClaimDocumentRepository.cs`
- Test: `backend/tests/ClaimsModule.PersistenceTests/ClaimDocumentTests.cs` (new, or add to an existing file covering `ClaimDocument` if one exists — check first)

**Interfaces:**
- Consumes: `ClaimDocument` (existing, `ClaimsModule.Domain`), `IUnitOfWork` (existing).
- Produces: `IClaimDocumentRepository.Add(ClaimDocument document) : void` and `IClaimDocumentRepository.CommitChangesAsync(CancellationToken = default) : Task`, alongside the existing `ListByClaimIdAsync`. Consumed by Task 5's handler.

- [ ] **Step 1: Write the failing test** — insert a `ClaimDocument` via `repository.Add(...)` + `CommitChangesAsync()`, then re-read it via a fresh `ClaimsDbContext` and assert the row exists with the fields set.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement.** Inject `IUnitOfWork` into `ClaimDocumentRepository`'s constructor (matching every other write-capable repository's pattern); `Add` calls `dbContext.ClaimDocuments.Add(document)`; `CommitChangesAsync` delegates to `IUnitOfWork.CommitChangesAsync`.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.PersistenceTests --filter "FullyQualifiedName~ClaimDocument"`
- [ ] **Step 5: Commit**

---

### Task 3: `LocalFileSystemStorageService` + static-file fallback wiring

**Files:**
- Create: `backend/src/ClaimsModule.Infrastructure/Storage/LocalFileSystemStorageService.cs`
- Create: `backend/src/ClaimsModule.Infrastructure/Storage/LocalStorageOptions.cs`
- Modify: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (`services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"))`; register `LocalFileSystemStorageService` as scoped)
- Modify: `backend/src/ClaimsModule.API/Program.cs` (after `var app = builder.Build();`: read the local storage root from `app.Configuration["LocalStorage:RootPath"]` — default to a sensible relative path if unset — `Directory.CreateDirectory` it, then `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(rootPath), RequestPath = "/uploads" })`)
- Modify: `backend/src/ClaimsModule.API/appsettings.json` (add `LocalStorage:RootPath`, e.g. `"uploads"`)
- Test: `backend/tests/ClaimsModule.UnitTests/LocalFileSystemStorageServiceTests.cs` (new)

**Interfaces:**
- Consumes: `IStorageService` (Task 1).
- Produces: `LocalFileSystemStorageService` — concrete `IStorageService` implementation, `LocalStorageOptions { string RootPath }`. Consumed by Task 4's provider-selection factory.

- [ ] **Step 1: Write the failing test** in `LocalFileSystemStorageServiceTests` — instantiate `new LocalFileSystemStorageService(Options.Create(new LocalStorageOptions { RootPath = <a fresh temp directory> }))` directly (no DI container, no HTTP):
  - `Uploading_writes_the_file_under_organisationId_claimId_and_returns_an_uploads_prefixed_path`: call `UploadAsync` with known bytes; assert the file exists on disk at `{tempRoot}/{organisationId}/{claimId}/{fileName}` with matching content, and the returned path starts with `/uploads/`.
  - `GetDownloadUrlAsync_returns_the_same_path_UploadAsync_returned`: round-trip the returned path through `GetDownloadUrlAsync` and assert it's unchanged (the local provider's "URL" is just that path — Resolved Ambiguity 4).
  - Clean up the temp directory in a `finally`/`IDisposable` pattern so the test doesn't leak files across runs.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** `LocalFileSystemStorageService.UploadAsync` (build `Path.Combine(options.Value.RootPath, organisationId.ToString(), claimId.ToString(), sanitisedFileName)`, `Directory.CreateDirectory` the parent, write `content` to it, return `"/uploads/{organisationId}/{claimId}/{sanitisedFileName}"` with forward slashes regardless of OS) and `GetDownloadUrlAsync` (return `blobPath` unchanged — it's already a servable path once static-file middleware is wired). Wire `Program.cs`'s static-file middleware per the Files list, after `var app = builder.Build();`, using `app.Configuration` not `builder.Configuration` (Resolved Ambiguity 4's "too early" trap).
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.UnitTests --filter "FullyQualifiedName~LocalFileSystemStorageServiceTests"`
- [ ] **Step 5: Commit**

---

### Task 4: `AzureBlobStorageService` + provider-selection wiring

**Files:**
- Modify: `backend/src/ClaimsModule.Infrastructure/ClaimsModule.Infrastructure.csproj` (add `Azure.Storage.Blobs` via `dotnet add package`)
- Create: `backend/src/ClaimsModule.Infrastructure/Storage/AzureBlobStorageService.cs`
- Modify: `backend/src/ClaimsModule.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (register `BlobServiceClient` as a lazily-resolved singleton from `ConnectionStrings:AzureBlobStorage`; register `AzureBlobStorageService` as scoped; register `IStorageService` via the lazy provider-selection factory described in Resolved Ambiguity 3, choosing between Task 3's `LocalFileSystemStorageService` and this task's `AzureBlobStorageService`)
- Modify: `backend/src/ClaimsModule.API/appsettings.json` (add `ConnectionStrings:AzureBlobStorage: ""`, `StorageProvider: "AzureBlob"`)
- Modify: `backend/tests/ClaimsModule.ApiIntegrationTests/ApiFixture.cs` (wire `ConnectionStrings:AzureBlobStorage` = `AzuriteConnectionString` and `StorageProvider` = `"AzureBlob"` into the in-memory config override)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/AzureBlobStorageServiceTests.cs` (new)

**Interfaces:**
- Consumes: `IStorageService` (Task 1), `LocalFileSystemStorageService` (Task 3, for the selection factory), `Azure.Storage.Blobs.BlobServiceClient` (SDK).
- Produces: `AzureBlobStorageService` — concrete `IStorageService` implementation; the final `IStorageService` DI registration both Tasks 5 and 6's handlers resolve. Consumed by DI registration only (handlers depend on `IStorageService`, never a concrete type).

- [ ] **Step 1: Write the failing test** in `AzureBlobStorageServiceTests`:
  - `Uploading_then_generating_a_download_URL_round_trips_the_exact_bytes` (Review Focus item 4): resolve `IStorageService` from `Fixture.Services`, call `UploadAsync` with known bytes and a sanitised filename, call `GetDownloadUrlAsync` with the returned `BlobPath`, then fetch that URL with a **plain, unauthenticated** `HttpClient` (not `Fixture.CreateAuthenticatedClient` — a SAS URL must work without your API's own auth) and assert the downloaded bytes equal what was uploaded.
  - `The_generated_SAS_URL_expires_in_about_one_hour`: inspect the returned URL's `se` (signed-expiry) query parameter (or re-parse it via `Azure.Storage.Sas.BlobSasBuilder`/`UriBuilder` + `Uri.Query`) and assert it's within a few minutes of `DateTimeOffset.UtcNow.AddHours(1)`.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** `AzureBlobStorageService.UploadAsync` (get/create the `"claim-documents"` container via `BlobServiceClient.GetBlobContainerClient` + `CreateIfNotExistsAsync`, blob name `"{organisationId}/{claimId}/{sanitisedFileName}"`, upload with `BlobHttpHeaders.ContentType` set, return `"claim-documents/{blobName}"`) and `GetDownloadUrlAsync` (split the returned `blobPath` on the first `/` into container + blob name, build a `BlobSasBuilder` with `Resource = "b"`, `BlobSasPermissions.Read`, `ExpiresOn = UtcNow.AddHours(1)`, call `BlobClient.GenerateSasUri`). Wire DI registrations and `ApiFixture`/`appsettings.json` config per the Files list.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~AzureBlobStorageServiceTests"`
- [ ] **Step 5: Commit**

---

### Task 5: `UploadClaimDocumentCommand` + controller action

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/UploadClaimDocument/UploadClaimDocumentCommand.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/UploadClaimDocument/UploadClaimDocumentCommandValidator.cs`
- Create: `backend/src/ClaimsModule.Application/Claims/UploadClaimDocument/UploadClaimDocumentCommandHandler.cs`
- Create: `backend/src/ClaimsModule.API/Models/UploadClaimDocumentRequest.cs` (API-layer-only multipart-bound model)
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs` (add `POST {id}/documents`)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/UploadClaimDocumentTests.cs` (new)

**Interfaces:**
- Consumes: `IClaimRepository.GetByIdAsync` (existing), `IClaimDocumentRepository.Add`/`CommitChangesAsync` (Task 2), `IStorageService.UploadAsync` (Task 1/3/4), `DocumentFileNameSanitizer.Sanitize` (Task 1), `ClaimDocumentDto` (Task 1), `IAuditLogService.LogAsync` (existing).
- Produces: `UploadClaimDocumentCommand : IRequest<ClaimDocumentDto>` with properties `Guid ClaimId`, `string DocumentType`, `string FileName`, `string ContentType`, `long FileSizeBytes`, `Stream Content`, `string? Notes`. Not consumed by any later task in this plan.

- [ ] **Step 1: Write the failing tests** in `UploadClaimDocumentTests` (use `MultipartFormDataContent` + `StreamContent` against `POST /api/claims/{id}/documents`):
  - `Uploading_a_PDF_creates_the_document_and_a_DOCUMENT_UPLOADED_audit_entry`: upload a small byte array as `application/pdf`; assert `201`, the response `ClaimDocumentDto` has a non-empty `DownloadUrl`, and `GET /api/claims/{id}/audit` contains exactly one `DOCUMENT_UPLOADED` entry (`AuditLogEntryDto` only exposes `CreatedAt`/`EventType`/`Description` today — assert on those, don't extend the DTO speculatively just to check `RelatedEntityId`).
  - `Uploading_a_disallowed_content_type_is_rejected` (Review Focus item 5): upload with `Content-Type: application/x-msdownload` (or any type outside the allowlist); assert `422` with a field error on the content-type field.
  - `Uploading_a_file_over_50MB_is_rejected`: a `>50MB` byte array with an allowed content type; assert `422` with a field error on the size field. (Keep the test array just over the limit, not egregiously large, to keep the test fast.)
  - `Uploading_to_a_nonexistent_claim_returns_404` (Review Focus item 2).
  - `A_path_traversal_filename_is_sanitised_before_reaching_storage` (Review Focus item 1): upload with a filename like `"../../etc/passwd"`; assert `201` and that the response's `DocumentName`/the underlying blob path contains no `..` or path separators from the original filename.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement.** `UploadClaimDocumentCommandValidator`: `RuleFor(x => x.ContentType).Must(allowlist.Contains)`, `RuleFor(x => x.FileSizeBytes).LessThanOrEqualTo(50 * 1024 * 1024)`, `RuleFor(x => x.DocumentType).NotEmpty()`. `UploadClaimDocumentCommandHandler.Handle`: load the claim (404 if missing) → sanitise the filename → `storageService.UploadAsync(claim.OrganisationId, claim.ClaimId, sanitisedFileName, request.Content, request.ContentType, ct)` → build and `Add` a `ClaimDocument` (store the *original* `FileName` as `DocumentName`, the returned path as `BlobPath`) → `auditLogService.LogAsync(new AuditLogEntry(claim.ClaimId, "DOCUMENT_UPLOADED", $"Document uploaded: {request.FileName}.", null, null, document.ClaimDocumentId, "ClaimDocument"))` → `CommitChangesAsync` → `storageService.GetDownloadUrlAsync(document.BlobPath, ct)` → map to `ClaimDocumentDto`. Controller action: `[HttpPost("{id:guid}/documents")] [RequestSizeLimit(52_428_800)] [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]`, binds `[FromForm] UploadClaimDocumentRequest` (with `IFormFile File`, `string DocumentType`, `string? Notes`), builds the command from `request.File.OpenReadStream()`/`.FileName`/`.ContentType`/`.Length`, returns `201`.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~UploadClaimDocumentTests"`
- [ ] **Step 5: Commit**

---

### Task 6: `ListClaimDocumentsQuery` + controller action

**Files:**
- Create: `backend/src/ClaimsModule.Application/Claims/ListClaimDocumentsQuery.cs`
- Modify: `backend/src/ClaimsModule.API/Controllers/ClaimsController.cs` (add `GET {id}/documents`)
- Test: `backend/tests/ClaimsModule.ApiIntegrationTests/ListClaimDocumentsTests.cs` (new)

**Interfaces:**
- Consumes: `IClaimRepository.GetByIdAsync` (existing), `IClaimDocumentRepository.ListByClaimIdAsync` (existing), `IStorageService.GetDownloadUrlAsync` (Task 1/3/4), `ClaimDocumentDto` (Task 1).
- Produces: `ListClaimDocumentsQuery(Guid ClaimId) : IRequest<List<ClaimDocumentDto>>`. Terminal — not consumed elsewhere in this plan.

- [ ] **Step 1: Write the failing tests** in `ListClaimDocumentsTests`:
  - `Listing_documents_for_a_claim_with_none_returns_an_empty_list` (Review Focus item 3).
  - `Listing_after_an_upload_returns_a_working_download_URL` (Review Focus item 4, end-to-end this time): upload via the real endpoint, then `GET {id}/documents`, assert one entry whose `DownloadUrl` — fetched with a plain `HttpClient` — returns the exact bytes uploaded.
  - `Listing_documents_for_a_nonexistent_claim_returns_404`.
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** `ListClaimDocumentsQueryHandler.Handle`: load the claim (404 if missing) → `ListByClaimIdAsync` → for each document, `await storageService.GetDownloadUrlAsync(document.BlobPath, ct)` → map to `ClaimDocumentDto`. Controller action: `[HttpGet("{id:guid}/documents")]`, returns `200` with the list.
- [ ] **Step 4: Run, verify pass.** `dotnet test backend/tests/ClaimsModule.ApiIntegrationTests --filter "FullyQualifiedName~ListClaimDocumentsTests"`
- [ ] **Step 5: Commit**

---

## Self-Review

**Spec coverage:** all 8 user stories map to a task — 1/2/6 → Task 5; 3 → Task 5's validator; 4/8 → Task 6 + Task 3's Azurite wiring; 5 → Task 4; 7 → Task 5's audit write.

**Proportion check:** six tasks, each with named tests and exact signatures; implementation bodies are described as an ordered sequence of calls (reusing every existing pattern — repository load-then-404, `IAuditLogService.LogAsync`, FluentValidation field rules) rather than written out, except where the spec itself leaves a real choice (the local URL scheme, the lazy-DI-selection mechanism) — those get a sentence of reasoning, not a code block.

## Final steps (not a plan task — part of the `/implement` workflow wrapping this plan)

- [ ] Run `dotnet build` and `dotnet test backend/tests/ClaimsModule.UnitTests` after every task (fast loop).
- [ ] Run the full `dotnet test` (Docker required) once after Task 6.
- [ ] Dispatch a fresh whole-branch code review (most capable model), same process as Slices B, C, and D's final reviews.
- [ ] Commit (already happening per-task above) — no separate final commit needed unless the review produces fixups.
