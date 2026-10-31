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

        return services;
    }
}
