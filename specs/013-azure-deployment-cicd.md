---
title: "Slice M: Azure Deployment and CI/CD"
labels: ["ready-for-agent"]
status: open
---

## Problem Statement

Nothing is deployed. §19–20 require a publicly accessible Azure deployment (App Service backend with Swagger reachable, Static Web App or second App Service frontend, Azure SQL, Blob Storage, optionally Key Vault), test credentials for at least a handler and a supervisor, and a basic GitHub Actions / Azure Pipelines definition that builds the backend, runs EF migrations, builds the frontend and deploys both (manual trigger acceptable). The repository has no remote, no pipeline and no infrastructure definition. The owner's budget is **$2/month on a Pay-As-You-Go subscription** (no hard spending cap), so the design itself must keep spend at effectively zero.

## Solution

Host the code on a private GitHub repository shared with the reviewers; define every Azure resource in Bicep, each pinned to its free or capped setting; provision and deploy from manually triggered GitHub Actions authenticated to Azure with OIDC; keep secrets in Key Vault read through the API's managed identity; run EF migrations from the pipeline (never at app startup); add CI on every push. One-time human-only setup (Azure login, app registration, role assignments) is a guided Cloud Shell script. Decisions and their consequences are recorded in [ADR-0012](../docs/adr/0012-free-tier-azure-deployment.md).

## User Stories

1. As a DICEUS reviewer, I want the source in a GitHub repository `ponatosik/diceus-claims-module` (default branch `main`, full history) that I'm invited to, so that I can read the code and the pipeline definitions (§20.1 "public or shared repository").
2. As the project owner, I want one guided script, run in Azure Cloud Shell, that performs the steps only I can do — confirm the subscription and print its offer type, register resource providers, create the resource group, create an Entra app registration with a federated credential for `repo:ponatosik/diceus-claims-module:ref:refs/heads/main`, grant it Contributor, Key Vault Secrets Officer, and Role Based Access Control Administrator **conditioned to assigning only Key Vault Secrets User** on the resource group — then sets the repo variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `BUDGET_ALERT_EMAIL` with `gh`, and can trigger provision and deploy, so that everything after it runs from GitHub Actions with no stored secrets.
3. As the project owner, I want all infrastructure in Bicep (`infra/main.bicep` + modules) deployed to resource group `rg-diceus-claims` in West Europe by a manual `provision.yml` workflow, so that the deployment is reproducible from the repo (§20.2 "how to reproduce the deployment").
4. As the project owner, I want every resource on a setting that cannot exceed the $2 budget:
   - App Service plan **F1 Free** (Linux) hosting the API (.NET 9 runtime stack);
   - Static Web App **Free**;
   - Azure SQL logical server with two databases (main, Hangfire) on the **free offer**, `useFreeLimit: true`, `freeLimitExhaustionBehavior: AutoPause`, local backup redundancy, no auditing;
   - Storage account Standard **LRS**, Hot, no geo-replication, soft-delete or versioning; private blob container `claim-documents`;
   - Key Vault Standard;
   - **no** Application Insights, Log Analytics or Defender plans.
5. As the project owner, I want a **$2/month budget** on the resource group with email alerts to the owner at 50% and 100% of actual and 100% of forecast spend, so that any unexpected cost is visible quickly — knowing that on Pay-As-You-Go a budget alerts but does not stop spending.
6. As the security-conscious owner, I want the SQL admin password (generated once), both SQL connection strings, the blob connection string and a random JWT signing key stored only in Key Vault, and the API reading them through **App Service Key Vault references** via its system-assigned managed identity (Key Vault Secrets User role), so that no secret is in the repo, in workflow logs, in Bicep outputs or in plain app settings.
7. As a reviewer, I want the API's non-secret settings set as app settings — `StorageProvider=AzureBlob`, `Jwt__Issuer`, `Jwt__Audience`, `DevAuth__Enabled=true` (ADR-0010's demo deployment), `Cors__AllowedOrigins__0=<Static Web App URL>`, `ASPNETCORE_ENVIRONMENT=Production` — so that Swagger, the demo users and the cross-origin frontend all work on the public URLs (§20.3).
8. As the project owner, I want a `ci.yml` workflow on every push (any branch) and pull request that builds the backend and runs **all three** test projects (Testcontainers on GitHub's Ubuntu runners), and for the frontend runs lint, format check, unit tests and the production build, so that every change is verified before it can be deployed.
9. As the project owner, I want a manual `deploy.yml` (`workflow_dispatch`) that: builds and publishes the API; builds the **EF migration bundle**; opens a **temporary SQL firewall rule for the runner's IP**, applies migrations to the main database, and removes the rule in an always-run step; zip-deploys the API; builds the frontend and writes `assets/config.json` with the API's public URL; deploys it to the Static Web App; and runs a smoke test (Swagger reachable, a demo token can be minted, `GET /api/claims` returns 200) — satisfying NFR-AZ-03.
10. As a reviewer, I want `deploy.yml`'s optional `seedDemoClaims` input to create about six claims **through the deployed API** as the demo users — a mix of statuses, reserves (auto-approved and pending), parties and a document — skipping itself if claims already exist, so that the live demo isn't an empty dashboard and every seeded claim has a genuine audit trail.
11. As a reviewer, I want the README to give the public frontend URL, the Swagger URL and a **Test credentials** section (the three demo users and the "Act as" switcher), and to explain the free tier's cold start (~10–20s first request after idle), so that I know what to expect in the live session (§20.3).
12. As a reviewer, I want ARCHITECTURE.md's "Azure architecture" section to describe the deployed topology and how the services interact, replacing the "nothing is deployed" text (§20.4).

## Implementation Decisions

- **Repo**: **private**, shared with the reviewers as collaborators (changed from the planned public repo at implementation time: the repo contains DICEUS's assessment PDFs and `REQUIREMENTS.md`, which reproduces them verbatim). `master` renamed to `main`; branch protection not required.
- **Bicep layout**: `infra/main.bicep` (resource-group scope) with modules per concern (app service, static web app, sql, storage, key vault, budget). Globally unique names get a short `uniqueString(resourceGroup().id)` suffix: e.g. `app-diceus-claims-api-<sfx>`, `stapp-diceus-claims`, `sql-diceus-claims-<sfx>`, `stdiceusclaims<sfx>`, `kv-diceus-claims-<sfx>`. Static Web App region must be one SWA supports (West Europe is).
- **Secret generation**: the SQL admin password and JWT signing key are generated by the provision workflow **only if the Key Vault secret doesn't exist yet**, then written to Key Vault — never re-generated on re-provision (a Bicep `newGuid()` default would rotate them every run) and never printed. Connection strings are composed in Bicep from resource properties and written as Key Vault secrets.
- **Azure SQL**: SQL authentication (admin login + Key Vault password). No "Allow Azure services" rule (it admits every Azure customer's IPs): `provision.yml` maintains one firewall rule per API outbound IP; the pipeline's runner-IP rule exists only during migration. Passwordless Entra auth is the recorded upgrade (ADR-0012).
- **Hangfire database**: schema created by Hangfire at API startup, as locally; not part of the EF bundle.
- **Blob storage**: connection-string (account key) auth, unchanged code path that generates 1-hour SAS URLs; managed-identity / user-delegation SAS is the recorded upgrade (ADR-0012).
- **Frontend hosting**: Static Web App Free cannot link a backend, so calls are cross-origin; `staticwebapp.config.json` provides the SPA navigation fallback and `no-store` caching for `/assets/config.json`.
- **API deployment**: `dotnet publish` output zip-deployed with `azure/webapps-deploy` under OIDC (`azure/login`); no container registry.
- **F1 implications accepted**: no Always On — the API sleeps after ~20 idle minutes, Hangfire (outbox dispatcher, GL posting, SLA job) runs only while awake; 60 CPU-minutes/day quota. Sleeping also lets the serverless databases pause, which is what keeps them inside the free allowance.
- **No kill switch**: a budget-triggered Automation runbook was rejected (cost data lags 8–24h; the resource settings already make overspend practically impossible).
- **Workflows authenticate** via `azure/login` with OIDC (`id-token: write`), using the three repo variables; the Static Web App deployment token is read at deploy time with `az staticwebapp secrets list` rather than stored as a repo secret.

## Testing Decisions

- `ci.yml` is itself the regression gate: all backend tests (unit, persistence, API integration with Testcontainers) and frontend lint/format/unit/build must pass.
- `deploy.yml` ends with an automated **smoke test** against the public URLs (Swagger page 200; `POST /api/dev/auth/token` returns a token; authenticated `GET /api/claims` 200; frontend `index.html` 200 and `assets/config.json` names the API URL). A failing smoke test fails the run.
- `provision.yml` runs `az deployment group what-if` before `create` and prints it, so infra changes are visible in the run log.
- Bicep is validated in CI with `az bicep build` (no Azure login needed).
- Manual verification after the first deploy: the Playwright suite can be pointed at production with `E2E_BASE_URL` — **not** part of the pipeline, because it writes test claims into the demo database.

## Out of Scope

- Custom domains, TLS certificates beyond the platform defaults, multiple environments (staging), slots.
- Scaling, Always On, paid tiers (one-line Bicep changes, deliberately not taken under the $2 budget).
- Monitoring/alerting beyond the budget alert (no Application Insights / Log Analytics to stay at $0).
- Exposing the Hangfire dashboard (unchanged decision).
- Passwordless SQL and managed-identity blob access (recorded upgrades, ADR-0012).

## Further Notes

- Steps only the owner can do (Azure sign-in, subscription, app registration) are delivered as a guided Cloud Shell script built with the `wizard` skill; the owner then runs `gh variable set …` locally with the printed values.
- If the free SQL allowance (100,000 vCore-seconds per database per month) is exhausted, that database pauses until the 1st of the next month and the app stops working — acceptable for a review period with an idle-sleeping API; documented in the README.
- Cost figures used in planning (free-offer limits, F1 quotas, ~$0.02/GB-month blob storage) come from current public listings; confirm in the Azure pricing calculator for West Europe.
