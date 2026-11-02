---
title: "Slice L: Claim Detail — Documents and Audit Log Tabs"
labels: ["ready-for-agent"]
status: open
blocked_by: ["010"]
---

## Problem Statement

Users cannot upload or download claim documents, or review the claim's append-only audit trail, from the UI (§11.3 Tabs 4 and 5).

## Solution

A Documents tab over `GET/POST /api/claims/{id}/documents` with progress-reporting uploads and fresh-URL downloads, and a paginated, read-only Audit Log tab over `GET /api/claims/{id}/audit` with entity links into the other tabs.

## User Stories

1. As a user, I want a document list showing name, type, upload date, uploader and file size (§11.3).
2. As a user, I want Upload to open the file picker; after choosing a file, a small dialog asks for Document Type (PoliceReport, MedicalReport, Invoice, Photo, Other) and optional Notes, then uploads as multipart to `POST /api/claims/{id}/documents` with a progress indicator (§11.3).
3. As a user, I want files over 50MB or outside the backend's allowlist rejected before upload with a clear message (the server still validates and its 422 is shown if it disagrees).
4. As a user, I want Download to call the API for a fresh SAS URL — re-fetching `GET /documents` and taking that document's `downloadUrl` — and open it in a new tab (§11.3, BR-D-02), so that a list loaded more than an hour ago never hands out an expired URL. The tab is opened synchronously on click and navigated once the URL arrives, so popup blockers don't interfere.
5. As a user, I want the Audit Log tab to list every event in reverse-chronological order, each with timestamp, event-type badge, description, user display name, and a related-entity link when applicable (§11.3).
6. As a user, I want related-entity links to navigate within the same claim to the matching tab with the row highlighted (`ReserveHistory` → Reserves, `ClaimParty` → Parties, `ClaimDocument` → Documents); claim-level entries have no link.
7. As a user, I want the audit log paginated (Material paginator) with no edit or delete affordance anywhere (§11.3, AUD rules).

## Implementation Decisions

- Upload uses `HttpClient` with `reportProgress: true, observe: 'events'` inside `DocumentsApi`; the component only consumes a progress signal from the store.
- The client-side allowlist and size limit are constants mirroring `UploadClaimDocumentCommandValidator`; the server remains authoritative.
- Event-type badge colours grouped by family (claim / status / reserve / document / party / system) rather than one colour per event type.
- The entity-type → tab mapping lives in one helper shared with Slices J/K's highlight handling.

## Testing Decisions

- **Unit**: entity-type → tab/highlight mapping; client-side file validation; `DocumentsApi` upload emits progress then the created DTO (`HttpTestingController`).
- **Component**: upload dialog requires a type; download triggers a list re-fetch and opens the returned URL; audit tab renders no edit/delete controls.
- **E2E**: upload a small PDF as handler → appears in the list and a `DOCUMENT_UPLOADED` audit entry links back to it.

## Delivery Notes

- Local-filesystem download URLs are API-relative (`/uploads/…`) and are resolved against the
  runtime `apiBaseUrl`; Azure SAS URLs are absolute and used as-is. The API serves local files as
  attachments, so the new tab becomes a file download.
- The Documents and Audit tabs are lazy (`matTabContent`), like Reserves.
- Arriving via an audit link (`?tab=…&highlight=…`) used to lose the highlight, because the tab group
  reports programmatic switches too; the page now only rewrites the URL for a user's own tab change.
- Found while building this slice: audit descriptions rendered money as `¤9,000.00` on a
  culture-invariant host — fixed in the backend (`AuditMoney.Format`, always en-US).

## Out of Scope

- Document deletion or preview (not in §10/§11).
- Audit log filtering/search (not in §11.3).
