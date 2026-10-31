using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    // DbSets are added as the domain model is implemented (see wayfinder decision log / ADRs
    // in docs/adr/ for the entity design decisions already made).
}
