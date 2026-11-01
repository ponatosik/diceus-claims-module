using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/reference/claim-statuses — lists every status with its valid next-status transitions (§4.2).</summary>
public record ListClaimStatusesQuery : IRequest<List<ClaimStatusDto>>;

public record ClaimStatusDto(string Status, List<string> ValidNextStatuses);

public class ListClaimStatusesQueryHandler : IRequestHandler<ListClaimStatusesQuery, List<ClaimStatusDto>>
{
    public Task<List<ClaimStatusDto>> Handle(ListClaimStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ClaimStatus>()
            .Select(status => new ClaimStatusDto(
                status.ToString(),
                ClaimStatusTransitions.GetValidNextStatuses(status).Select(s => s.ToString()).ToList()))
            .ToList();

        return Task.FromResult(result);
    }
}
