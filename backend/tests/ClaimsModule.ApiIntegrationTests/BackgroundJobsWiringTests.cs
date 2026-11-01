using Hangfire;

namespace ClaimsModule.ApiIntegrationTests;

[Collection(ApiCollection.Name)]
public class BackgroundJobsWiringTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public void The_app_starts_with_Hangfire_storage_wired_to_the_separate_database()
    {
        var statistics = JobStorage.Current.GetMonitoringApi().GetStatistics();

        Assert.NotNull(statistics);
    }
}
