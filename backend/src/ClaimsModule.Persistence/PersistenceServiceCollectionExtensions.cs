using ClaimsModule.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ClaimsDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("ClaimsDatabase"),
                sql => sql.MigrationsAssembly(typeof(ClaimsDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimNumberGenerator, SqlSequenceClaimNumberGenerator>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IClaimReserveComponentRepository, ClaimReserveComponentRepository>();
        services.AddScoped<ICauseOfLossCodeRepository, CauseOfLossCodeRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IClaimDocumentRepository, ClaimDocumentRepository>();

        return services;
    }
}
