using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.TransitionClaimStatus;
using ClaimsModule.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(ApplicationAssemblyReference).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddAutoMapper(cfg => { }, [applicationAssembly]);

        // Wired at the MediatR pipeline level, not controller-level [ApiController] model validation
        // (§18.1 — reviewers check this explicitly).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<ClaimTransitionEvaluator>();
        services.AddScoped<ReserveTransactionRecorder>();

        return services;
    }
}
