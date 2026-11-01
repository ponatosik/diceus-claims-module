using ClaimsModule.Domain;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.PersistenceTests;

[Collection(DatabaseCollection.Name)]
public class OutboxMessageTests(DatabaseFixture fixture)
{
    private static readonly Guid OrganisationId = SeedData.OrganisationId;

    [Fact]
    public async Task An_unprocessed_outbox_message_round_trips_and_is_queryable_by_processed_state()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            OrganisationId = OrganisationId,
            Type = "EnqueueGLPostingJob",
            Payload = """{"reserveHistoryId":"22222222-2222-2222-2222-222222222222"}""",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await using var readContext = CreateDbContext();
        var unprocessed = await readContext.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();

        Assert.Single(unprocessed);
        Assert.Equal("EnqueueGLPostingJob", unprocessed[0].Type);
        Assert.Null(unprocessed[0].ProcessedAt);
    }

    [Fact]
    public async Task OutboxRepository_ListUnprocessedAsync_returns_only_rows_with_no_ProcessedAt()
    {
        await fixture.ResetAsync();
        await using var dbContext = CreateDbContext();

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            OrganisationId = OrganisationId,
            Type = "EnqueueGLPostingJob",
            Payload = """{"reserveHistoryId":"11111111-1111-1111-1111-111111111111"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            OrganisationId = OrganisationId,
            Type = "EnqueueGLPostingJob",
            Payload = """{"reserveHistoryId":"22222222-2222-2222-2222-222222222222"}""",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var repository = new OutboxRepository(dbContext, new UnitOfWork(dbContext));
        var unprocessed = await repository.ListUnprocessedAsync();

        Assert.Single(unprocessed);
        Assert.Contains("22222222-2222-2222-2222-222222222222", unprocessed[0].Payload);
    }

    private ClaimsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(fixture.ConnectionString).Options);
}
