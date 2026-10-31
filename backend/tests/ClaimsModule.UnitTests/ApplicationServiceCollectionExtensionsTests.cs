using ClaimsModule.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.UnitTests;

public class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplication_registers_mediatr_sender()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // MediatR requires ILoggerFactory; a real host (WebApplication) registers this by default.

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetService<ISender>();

        Assert.NotNull(sender);
    }
}
