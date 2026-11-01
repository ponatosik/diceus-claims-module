using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims/{id}/reserves — current balance per component plus full transaction history (§10.2).</summary>
public record ListClaimReservesQuery(Guid ClaimId) : IRequest<ReserveListDto>;

public class ListClaimReservesQueryHandler(IClaimRepository claimRepository, IClaimReserveComponentRepository reserveComponentRepository)
    : IRequestHandler<ListClaimReservesQuery, ReserveListDto>
{
    public async Task<ReserveListDto> Handle(ListClaimReservesQuery request, CancellationToken cancellationToken)
    {
        _ = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var components = await reserveComponentRepository.ListByClaimIdAsync(request.ClaimId, cancellationToken);
        var history = await reserveComponentRepository.ListHistoryByClaimIdAsync(request.ClaimId, cancellationToken);

        var componentTypesById = components.ToDictionary(c => c.ReserveComponentId, c => c.Component);

        var componentDtos = components
            .Select(c => new ReserveSummaryDto(c.ReserveComponentId, c.Component.ToString(), c.CurrentAmount, c.Status.ToString()))
            .ToList();
        var historyDtos = history
            .Select(h => ReserveTransactionMapper.ToDto(h, componentTypesById[h.ReserveComponentId]))
            .ToList();

        return new ReserveListDto(componentDtos, historyDtos);
    }
}
