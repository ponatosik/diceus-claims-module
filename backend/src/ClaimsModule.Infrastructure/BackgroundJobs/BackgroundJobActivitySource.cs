using System.Diagnostics;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Gives every background job execution its own <see cref="Activity"/>, so <c>AuditLogService</c>'s
/// existing <c>Activity.Current</c>-based correlation id (wayfinder ticket 8) picks up a fresh trace id
/// per job run automatically — no changes needed to AuditLogService itself. This is the documented
/// placeholder for wayfinder ticket 11 (still open): a background job's audit entries get a fresh
/// correlation id tied to that execution, not the originating HTTP request's.
/// </summary>
public static class BackgroundJobActivitySource
{
    public static readonly ActivitySource Source = new("ClaimsModule.BackgroundJobs");
}
