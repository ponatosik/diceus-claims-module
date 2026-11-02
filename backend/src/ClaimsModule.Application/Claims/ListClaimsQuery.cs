using ClaimsModule.Application.Common;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims — paginated, filterable claim list (§10.1) with per-row totalReserves for triage.</summary>
public record ListClaimsQuery(
    IReadOnlyCollection<ClaimStatus>? Statuses,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Guid? AssignedHandlerId,
    string? CauseOfLossCode,
    Guid? PolicyId,
    string? Search,
    int Page,
    int PageSize,
    string? AssignedHandlerName = null) : IRequest<PagedResult<ClaimSummaryDto>>;

public class ListClaimsQueryHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IUserDirectory userDirectory,
    ICauseOfLossCodeRepository causeOfLossCodeRepository)
    : IRequestHandler<ListClaimsQuery, PagedResult<ClaimSummaryDto>>
{
    public async Task<PagedResult<ClaimSummaryDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        // §11.1's "assigned handler (text search)": resolved to handler ids via the user directory.
        IReadOnlyCollection<Guid>? assignedHandlerIds = string.IsNullOrWhiteSpace(request.AssignedHandlerName)
            ? null
            : [.. await userDirectory.FindIdsByDisplayNameAsync(request.AssignedHandlerName.Trim(), cancellationToken)];

        var filter = new ClaimListFilter(
            request.Statuses, request.DateFrom, request.DateTo, request.AssignedHandlerId,
            request.CauseOfLossCode, request.PolicyId, request.Search, assignedHandlerIds);

        var (items, totalCount) = await claimRepository.ListAsync(filter, request.Page, request.PageSize, cancellationToken);

        var totals = await reserveComponentRepository.GetTotalReservesByClaimIdsAsync(
            items.Select(c => c.ClaimId), cancellationToken);

        var causeOfLossNames = await causeOfLossCodeRepository.GetNamesByCodesAsync(
            items.Where(c => c.LossEvent is not null).Select(c => c.LossEvent!.CauseOfLossCode), cancellationToken);

        var summaries = items.Select(claim => new ClaimSummaryDto(
            claim.ClaimId,
            claim.ClaimNumber,
            claim.ClientName,
            claim.PolicyNumber,
            claim.LossEvent?.LossDate ?? default,
            claim.Status.ToString(),
            totals.GetValueOrDefault(claim.ClaimId, 0m),
            claim.LossEvent?.CauseOfLossCode,
            claim.LossEvent is null ? null : causeOfLossNames.GetValueOrDefault(claim.LossEvent.CauseOfLossCode))).ToList();

        return new PagedResult<ClaimSummaryDto>(summaries, totalCount, request.Page, request.PageSize);
    }
}
