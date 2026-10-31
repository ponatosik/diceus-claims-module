# Per-aggregate repositories with commits delegated to a shared IUnitOfWork

NFR-ARCH-03 requires the Unit of Work pattern to coordinate persistence across repositories, and FNOL creation needs Claim + LossEvent + Parties + RiskObjects + an optional initial reserve to commit atomically in one transaction (§10.1). We use **one repository per aggregate root**, all sharing a single scoped `DbContext`. There is an explicit `IUnitOfWork` type that owns the actual `SaveChangesAsync`/transaction-commit logic; each repository's `CommitChangesAsync()` method is a thin pass-through that delegates to the shared `IUnitOfWork` instance. Application-layer handlers only ever touch repositories — `IUnitOfWork` is an implementation detail repositories share, never injected directly into a MediatR handler.

## Considered Options

- **Repositories call `DbContext.SaveChangesAsync()` directly**, with no distinct `IUnitOfWork` type, relying on the shared scoped `DbContext` as the implicit unit of work. Rejected because it doesn't give NFR-ARCH-03 a named, visible abstraction, and makes it easy for a future repository to accidentally own its own `DbContext` instance and silently break atomicity.
- **`IUnitOfWork` injected directly into handlers** alongside repositories, with handlers calling `_unitOfWork.CommitAsync()` themselves. Rejected to keep handler code simpler — one dependency surface (repositories), not two.

## Consequences

Every repository must be constructed with (or resolve) the same `IUnitOfWork`/`DbContext` instance within a given request scope — a repository that stands up its own `DbContext` would silently break cross-aggregate transaction atomicity with no compile-time signal.
