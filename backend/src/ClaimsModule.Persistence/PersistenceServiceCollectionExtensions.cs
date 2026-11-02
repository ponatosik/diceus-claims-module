using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<ClaimsDbContext>((sp, options) =>
            options
                .UseSqlServer(
                    configuration.GetConnectionString("ClaimsDatabase"),
                    sql => sql.MigrationsAssembly(typeof(ClaimsDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimNumberGenerator, SqlSequenceClaimNumberGenerator>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IClaimReserveComponentRepository, ClaimReserveComponentRepository>();
        services.AddScoped<ICauseOfLossCodeRepository, CauseOfLossCodeRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IClaimDocumentRepository, ClaimDocumentRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }
}
