using ClaimsModule.Application;
using ClaimsModule.Domain;

namespace ClaimsModule.Persistence;

public class ClaimReserveComponentRepository(ClaimsDbContext dbContext, IUnitOfWork unitOfWork) : IClaimReserveComponentRepository
{
    public void Add(ClaimReserveComponent component) => dbContext.ClaimReserveComponents.Add(component);

    public async Task CommitChangesAsync(CancellationToken cancellationToken = default) =>
        await unitOfWork.CommitChangesAsync(cancellationToken);
}
