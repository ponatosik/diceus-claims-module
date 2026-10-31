---
title: "Slice E: Document Management"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

A claims handler has no way to attach supporting documents (police reports, medical reports, invoices) to a claim, and the Angular Claim Detail screen's Documents tab (§11.3) has nothing to call.

## Solution

Implement document upload and listing against the `ClaimDocument` entity from Slice A, behind a provider-abstracted `IStorageService` (Azure Blob primary, local filesystem fallback, per §7.4/§13) with short-lived SAS URLs for retrieval.

## User Stories

1. As a claims handler, I want `POST /api/claims/{id}/documents` to accept a multipart file upload with a document type, store the bytes in Azure Blob Storage under `claim-documents/{organisationId}/{claimId}/{sanitisedFilename}`, and create a `ClaimDocuments` metadata row, so that supporting evidence is attached to the claim.
2. As a claims handler, I want the uploaded filename sanitised to remove path-traversal characters (BR-D-01) before it's used to build the blob path, so that a malicious or malformed filename can't write outside the intended container prefix.
3. As a claims handler, I want files outside an allowlist (PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV) rejected, and anything over a reasonable 50MB limit rejected, so that the upload endpoint isn't a dumping ground for arbitrary content.
4. As a claims handler, I want `GET /api/claims/{id}/documents` to list every document on the claim with a short-lived (1-hour TTL) SAS download URL per document (BR-D-02), so that the frontend can offer direct downloads without proxying bytes through the API.
5. As a developer running the solution locally without Azure Blob Storage configured, I want document storage to fall back automatically to the local filesystem under `/uploads/{organisationId}/{claimId}/` (BR-D-03), so that local development and CI don't require real Azure infrastructure.
6. As a developer switching storage providers, I want the choice made via `appsettings.json` (`StorageProvider: AzureBlob | LocalFileSystem`) behind a single `IStorageService` interface with `AzureBlobStorageService` and `LocalFileSystemStorageService` implementations, so that swapping providers never touches a command handler.
7. As a DICEUS reviewer, I want every document upload to write a `DOCUMENT_UPLOADED` audit entry with `RelatedEntityId` set to the new document's id, so that document activity is part of the claim's audit trail like everything else.
8. As the `ClaimsModule.ApiIntegrationTests` project, I want to exercise a real upload against the Azurite container ADR-0009 already wired into `ApiFixture`, so that the Azure Blob code path is proven against something that behaves like real Azure Blob Storage, not just the local filesystem fallback.

## Implementation Decisions

- **`IStorageService` lives in `ClaimsModule.Application`** (the interface) with implementations in `ClaimsModule.Infrastructure` (`AzureBlobStorageService`, `LocalFileSystemStorageService`) — matching the existing pattern for `IAuditLogService`/repository interfaces and the architecture-fitness test's enforcement that `Application` never depends on `Infrastructure` directly.
- **MediatR command**: `UploadClaimDocumentCommand` (multipart form data bound at the API layer, passed into the command as a stream/byte array plus metadata). **Query**: `ListClaimDocumentsQuery`.
- **Blob path**: `claim-documents/{organisationId}/{claimId}/{sanitisedFilename}`, with filename sanitisation implemented once (shared between both `IStorageService` implementations, not duplicated) stripping path-traversal sequences and non-filename-safe characters.
- **Validation**: a FluentValidation validator on `UploadClaimDocumentCommand` checks content-type against the allowlist and file size against the 50MB limit — these are field-level (the uploaded file itself), so they produce the standard `errors` shape from Slice B's validation pipeline, not `blockingConditions`.
- **SAS URL generation**: `AzureBlobStorageService` generates a 1-hour-TTL SAS URL via the Azure SDK; `LocalFileSystemStorageService`'s equivalent "short-lived URL" is a locally-servable path (exact mechanism — e.g. a signed local token or a dev-only unauthenticated static-file route — decided during implementation, since `REQUIREMENTS.md` doesn't specify the local fallback's URL scheme in detail, only that document bytes must never be proxied through the main API for the Azure case).
- **Document bytes are never read into the API's own response body** for download — only the metadata list endpoint returns URLs; the actual bytes are served directly by Blob Storage (or the local fallback) via the generated URL.

## Testing Decisions

- **Seam: `ClaimsModule.ApiIntegrationTests`**, using the Azurite container ADR-0009 already provisions in `ApiFixture`. One HTTP-level upload-then-list test proves the full path: multipart upload → blob written → metadata persisted → audit entry written → list endpoint returns a working SAS URL.
- Validation-boundary tests (file type allowlist, size limit) belong at this same seam rather than as isolated unit tests, since the interesting behavior is "the API rejects this upload," not "a validator object returns false" — consistent with Slice B's "ideal number of seams is one" approach.
- The local-filesystem fallback path is worth a separate test run (or a parametrized test) against `StorageProvider: LocalFileSystem` configuration, proving the provider switch genuinely changes behavior without touching handler code.
- Prior art: Slice B/C/D's `ApiFixture`-based upload-then-assert pattern applies directly; this slice additionally needs `fixture.AzuriteConnectionString`, which `ApiFixture` already exposes but doesn't yet wire into the running app's configuration (today only `ConnectionStrings:ClaimsDatabase` is overridden in `ConfigureWebHost` — this slice is where the Azure Blob connection string override gets added).

## Out of Scope

- Document deletion — not in `REQUIREMENTS.md`'s API table at all; documents are append-only once uploaded.
- Virus scanning or content inspection beyond MIME-type allowlisting.
- Any frontend upload-progress UI — frontend concern.

## Further Notes

This is the last backend slice in the current decision log's scope. Once it's implemented, the only thing left from the wayfinder map is ticket 11 (correlation-id for background jobs) and whatever `REQUIREMENTS.md` §19–20 work (Azure deployment, CI/CD) a future map takes on.
