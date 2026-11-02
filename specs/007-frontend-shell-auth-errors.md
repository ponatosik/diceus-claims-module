---
title: "Slice G: Frontend Shell, Demo Auth, Error Handling"
labels: ["ready-for-agent"]
status: open
blocked_by: ["006"]
---

## Problem Statement

There is no frontend. Before any screen can be built, the Angular workspace needs the cross-cutting foundations §11.4 requires: lazy feature routing, a mock auth service with a role switcher whose tokens the real API accepts, an interceptor adding the Bearer token, centralized HTTP error handling surfaced as snackbars, loading conventions, a consistent Material theme, and a typed API layer — plus a docker-compose environment to run it all against.

## Solution

Scaffold `/frontend` on current stable Angular with standalone components and lazy route bundles, build the `core/`, `shared/` and `api/` foundations every feature slice uses, and add a root `docker-compose.yml` (SQL Server + API + frontend).

## User Stories

1. As a developer, I want an Angular workspace in `/frontend` (current stable Angular, standalone components, strict TS + strict templates, `npm`, Node LTS pinned in `.nvmrc` and `engines`, `angular-eslint` + Prettier), so that `npm install`, `ng serve`, `ng build`, `ng test`, `ng lint` all work as CLAUDE.md documents.
2. As a reviewer, I want three lazy-loaded features — `claims-list`, `fnol-intake`, `claim-detail` — each with its own `*.routes.ts` loaded via `loadChildren`, so that §11.4's lazy-loaded feature modules exist in modern form (ARCHITECTURE.md explains route bundles as the standalone-era equivalent of feature modules).
3. As a reviewer, I want the API base URL read from runtime config (`/assets/config.json`, loaded before bootstrap via an app initializer), so that one build artifact runs locally and on Azure.
4. As a user, I want a `MockAuthService` with the three Demo Users (fixed ids and display names matching the backend user directory) that obtains a JWT from `POST /api/dev/auth/token` for the selected user, so that every API call carries a token the real API validates.
5. As a user, I want the header to show the logged-in user's name and role and a role switcher (handler / supervisor / manager), with the selected user remembered across reloads, so that role-gated UI can be exercised (§11.4).
6. As the app, I want a functional auth interceptor adding `Authorization: Bearer <token>` to every API request (FR / §11.4), refreshing the token once and retrying on a `401`.
7. As a user, I want every API error normalized into a typed `ApiError` (`kind: validation | businessRule | notFound | conflict | unauthorized | server | network`, carrying `errors` and `blockingConditions`) and surfaced as a Material snackbar with appropriate severity (§11.4): 422 → warning snackbar *and* the error returned to the caller for inline display; 409 → error snackbar with a "Reload" action; 404/5xx/network → error snackbar; claim 404 → not-found page.
8. As a user, I want a consistent loading convention — a shared busy indicator / skeleton component and a pattern for disabling buttons while a request is pending (§11.4).
9. As a reviewer, I want an Angular Material 3 theme with a custom primary palette (deep teal/navy) and the seven claim status colours from §11.1 as design tokens, consumed by one shared `StatusBadge` component.
10. As a developer, I want a typed, hand-written API layer in `src/app/api/` — TS interfaces mirroring the backend DTOs and enums, and one service per resource (`ClaimsApi`, `ReservesApi`, `DocumentsApi`, `PoliciesApi`, `ReferenceApi`, `AuthApi`) — so that no component calls `HttpClient` directly (§11.4).
11. As a developer, I want shared formatting — money via `CurrencyPipe` (USD), dates in the user's local zone, ISO-8601 with offset on the wire — and pure domain helpers (`authorityTier(amount)` using `|amount|` and the §6.3 thresholds, `isInForce(policy, lossDate)`, `statusColour(status)`), so that every slice formats and decides consistently with the backend.
12. As a developer, I want `docker-compose.yml` at the repo root running SQL Server, the API (with `DevAuth:Enabled=true`, migrations applied on start) and the built frontend, plus an `ng serve` proxy config for the API in local dev.

## Implementation Decisions

- **Folder layout**: `src/app/core/` (auth, interceptors, error mapper, runtime config, shell/layout, not-found page), `src/app/shared/` (status badge, authority indicator, formatting, domain helpers, confirm dialog), `src/app/api/` (models + services), `src/app/features/{claims-list,fnol-intake,claim-detail}/` (routes, components, store).
- **State**: signals + feature-scoped store services provided at the route; RxJS only at the HTTP edge (`toSignal`/explicit subscriptions in stores). No NgRx.
- **Change detection**: OnPush everywhere (or zoneless if stable in the chosen version); new control flow (`@if`/`@for`).
- **Errors**: one `HttpInterceptorFn` maps `HttpErrorResponse` → `ApiError` and triggers the snackbar; callers receive the typed `ApiError` for inline handling. A shared `applyServerErrors(form, apiError)` helper maps `errors` keys (`Parties[0].Email` style) onto reactive-form controls.
- **Auth token storage**: in memory, re-fetched on reload for the remembered Demo User (only the selected user id is persisted). Demo User ids/names live in one constant file mirroring the backend seed.
- **Role checks** go through one `currentUser` signal + helpers (`canApproveReserves()`, `canApproveTier(tier)`, `canReopen()`); templates never compare role strings directly.
- Enums are TS string-literal unions matching `JsonStringEnumConverter` output.

## Testing Decisions

- **Unit (Vitest)**: error mapper (each status → `ApiError.kind`, `errors`/`blockingConditions` preserved), auth interceptor (header added, single retry on 401), `MockAuthService` (switching user re-fetches token), `authorityTier` boundaries (10,000 / 10,000.01 / 100,000 / 100,000.01 / negatives), `isInForce` boundaries, `applyServerErrors` path mapping. API services via `HttpTestingController` asserting URL/query/body shape.
- **E2E (Playwright)** project scaffolded here against docker-compose, with one smoke test: app loads, Demo User switch changes the header, an authenticated API call succeeds.
- Prior art: none in the frontend; these establish the patterns later slices follow.

## Out of Scope

- Azure Static Web App configuration and CI/CD (deferred, as for the backend).
- Real login, logout flows, token refresh beyond re-minting.
- Internationalisation; responsive layouts below 1280px (§11.4 minimum).
