namespace ClaimsModule.ApiIntegrationTests;

/// <summary>
/// Shared reset-before-each-test base for ApiIntegrationTests classes (see <see cref="ApiFixture.ResetAsync"/>).
/// Derive from this instead of re-implementing the Respawn reset call in every test class.
/// </summary>
public abstract class ApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// Exposed so derived test classes use this instead of also capturing their own constructor
    /// parameter — capturing the same fixture in both the derived class and this base triggers
    /// CS9107 on every test class otherwise.
    /// </summary>
    protected ApiFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
