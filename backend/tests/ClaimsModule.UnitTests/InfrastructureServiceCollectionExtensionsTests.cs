using ClaimsModule.Application;
using ClaimsModule.Application.Common;
using ClaimsModule.Infrastructure;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.UnitTests;

public class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_falls_back_to_LocalFileSystem_when_Azure_is_unconfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageProvider"] = "AzureBlob",
                ["ConnectionStrings:AzureBlobStorage"] = "",
                ["ConnectionStrings:HangfireDatabase"] = "",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        Assert.IsType<LocalFileSystemStorageService>(storageService);
    }
}
