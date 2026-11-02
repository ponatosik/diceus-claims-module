---
title: "Slice H: Claims List Dashboard"
labels: ["ready-for-agent"]
status: open
blocked_by: ["007"]
---

## Problem Statement

Users have no entry point to see, find, or open claims, or to start a new FNOL (§11.1).

## Solution

A paginated, filterable Material table backed by `GET /api/claims`, with all filter/paging state held in URL query params.

## User Stories

1. As a user, I want a paginated table with columns Claim Number, Policy Number, Client Name, Loss Date, Cause of Loss, Status (badge), Total Reserve Amount (§11.1).
2. As a user, I want status badges colour-coded per §11.1 (Draft grey, Open blue, UnderInvestigation orange, PendingPayment purple, Closed green, Reopened amber, Withdrawn dark grey) via the shared `StatusBadge`.
3. As a user, I want a filter bar with Status (multi-select), loss date range (from/to, Material date-range picker), assigned handler (text search) and Cause of Loss (dropdown from `/api/reference/cause-of-loss-codes`) (§11.1), plus the API's `search` (partial claim number or client name).
4. As a user, I want filters, page and page size reflected in the URL query string, so that a filtered view is bookmarkable, survives reload, and Back from a claim restores it.
5. As a user, I want text inputs debounced (300ms) and a filter change to reset to page 1.
6. As a user, I want the total claim count shown and a page size selector (10 / 20 / 50 / 100, default 20) (§11.1).
7. As a user, I want each row clickable, navigating to Claim Detail (§11.1).
8. As a user, I want a primary "Log New Claim" button opening the FNOL intake form (§11.1), available to every role (all roles have handler capabilities, §3).
9. As a user, I want a friendly empty state when no claims match, with a "clear filters" action (§11.1), and a skeleton loader while loading (§11.4).

## Implementation Decisions

- `ClaimsListStore` derives the API query from the route's query params (single source of truth); the filter form writes to the URL, never to the store directly.
- Sort order is the server's (newest reported first); no column sorting — §11.1 doesn't ask for it.
- Total Reserve Amount and Loss Date use the shared money/date formatting.

## Testing Decisions

- **Unit**: store's query-param ↔ API-query mapping (multi-status repeated params, date range, page reset on filter change).
- **Component (Angular Testing Library)**: empty state renders; row click navigates; status badge colour per status.
- **E2E**: covered by Slice I's FNOL test landing back on the list; no dedicated dashboard e2e beyond a filter-by-status check.

## Out of Scope

- Column sorting, export, bulk operations (§2 out of scope).
