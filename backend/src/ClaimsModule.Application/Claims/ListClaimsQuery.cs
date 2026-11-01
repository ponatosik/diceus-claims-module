using ClaimsModule.Application.Common;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims — paginated, filterable claim list (§10.1) with per-row totalReserves for triage.</summary>
public record ListClaimsQuery(
    ClaimStatus? Status,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? AssignedHandlerId,
    string? CauseOfLossCode,
    Guid? PolicyId,
    string? Search,
    int Page,
    int PageSize) : IRequest<PagedResult<ClaimSummaryDto>>;

public class ListClaimsQueryHandler(IClaimRepository claimRepository, IClaimReserveComponentRepository reserveComponentRepository)
    : IRequestHandler<ListClaimsQuery, PagedResult<ClaimSummaryDto>>
{
    public async Task<PagedResult<ClaimSummaryDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        var filter = new ClaimListFilter(
            request.Status, request.DateFrom, request.DateTo, request.AssignedHandlerId,
            request.CauseOfLossCode, request.PolicyId, request.Search);

        var (items, totalCount) = await claimRepository.ListAsync(filter, request.Page, request.PageSize, cancellationToken);

        var totals = await reserveComponentRepository.GetTotalReservesByClaimIdsAsync(
            items.Select(c => c.ClaimId), cancellationToken);

        var summaries = items.Select(claim => new ClaimSummaryDto(
            claim.ClaimId,
            claim.ClaimNumber,
            claim.ClientName,
            claim.PolicyNumber,
            claim.LossEvent?.LossDate ?? default,
            claim.Status.ToString(),
            totals.GetValueOrDefault(claim.ClaimId, 0m))).ToList();

        return new PagedResult<ClaimSummaryDto>(summaries, totalCount, request.Page, request.PageSize);
    }
}
