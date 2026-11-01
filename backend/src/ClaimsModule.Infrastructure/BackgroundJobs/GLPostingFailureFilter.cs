using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-specific glue for §12.1's failure path: once <see cref="PostGLReserveChangeJob"/>
/// exhausts its automatic retries and lands in <see cref="FailedState"/>, hands off to
/// <see cref="GLPostingFailureHandler"/>, which carries the actual business logic. Thin by design —
/// reviewed by eye like the rest of this codebase's startup wiring, not independently unit-tested
/// (see the background-jobs plan's Resolved Ambiguities for why).
/// </summary>
public class GLPostingFailureFilter(IServiceProvider services) : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState || context.BackgroundJob.Job.Type != typeof(PostGLReserveChangeJob))
        {
            return;
        }

        var reserveHistoryId = (Guid)context.BackgroundJob.Job.Args[0]!;

        using var scope = services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GLPostingFailureHandler>();

        // IApplyStateFilter has no async overload — this blocking call is Hangfire's own API shape,
        // not a code-quality lapse.
        handler.RecordFinalFailureAsync(reserveHistoryId, failedState.Exception.Message).GetAwaiter().GetResult();
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
