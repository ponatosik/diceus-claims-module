# Free-tier Azure deployment under a $2/month budget; migrations from the pipeline; secrets in Key Vault

The deployment runs on a Pay-As-You-Go subscription (no hard spending cap) with a $2/month budget, so cost safety comes from the design rather than from Azure: every resource is pinned to a setting that cannot bill beyond cents — App Service **F1 Free** for the API, Static Web Apps **Free**, two Azure SQL databases on the **free offer** with `AutoPause` when the monthly allowance is exhausted, Standard LRS storage, Key Vault Standard, and **no** Application Insights, Log Analytics or Defender plans — plus a $2 resource-group budget that emails the owner. We accept F1's consequences: no Always On, so the API sleeps after ~20 idle minutes (cold start of ~10–20s) and Hangfire's background work (outbox dispatcher, GL posting, SLA job) only runs while the API is awake. That same sleep is what lets the serverless databases auto-pause, keeping both inside their free allowance; if an allowance is ever exhausted the database pauses until next month rather than billing.

EF Core migrations are applied **by the deploy pipeline**, not by the API at startup (CLAUDE.md's rule): the workflow runs the migration bundle against Azure SQL through a **temporary firewall rule for the GitHub runner's IP**, removed in an always-run step. Secrets (SQL admin password, connection strings, blob connection string, JWT signing key) live only in **Key Vault**, read by the API through App Service Key Vault references and its managed identity; GitHub Actions authenticates to Azure with **OIDC** (no stored credentials).

## Considered Options

- **Basic B1 App Service (~$13/month, Always On)** and/or **Basic DTU databases (~$5/month each)** — rejected under the $2 budget; each is a one-line Bicep change if the cold start or the allowance ever becomes a problem.
- **Migrate on API startup** — rejected: contradicts the existing "the API never migrates itself" rule and couples deployment failures to app boot.
- **Opening Azure SQL to all IPs, or "Allow Azure services" for the runner** — rejected: GitHub runners aren't Azure services, and a permanent wide-open rule is a needless exposure.
- **Passwordless SQL (managed identity as a database user) and managed-identity blob access (user-delegation SAS)** — deferred, not rejected: more setup and a storage code change under deadline; the account key and SQL password never leave Key Vault meanwhile. These are the natural next steps.
- **A budget-triggered Automation runbook as a kill switch** — rejected: Cost Management data lags 8–24 hours and the resource settings already make overspend practically impossible.

## Consequences

The live demo's first request after idle is slow, and a GL posting completes only while someone is using the app (which is when it matters). Moving off the free tier is a configuration change, not a code change.
