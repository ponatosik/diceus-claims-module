using ClaimsModule.Application;
using ClaimsModule.Domain;

namespace ClaimsModule.Persistence;

public class ClaimRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IClaimRepository
{
    public void Add(Claim claim) => dbContext.Claims.Add(claim);

    public async Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        await unitOfWork.CommitChangesAsync(cancellationToken);
}
