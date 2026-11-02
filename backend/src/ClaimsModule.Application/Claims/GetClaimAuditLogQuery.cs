using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims/{id}/audit — the full, paginated, reverse-chronological append-only audit log for a claim (§10.1).</summary>
public record GetClaimAuditLogQuery(Guid ClaimId, int Page, int PageSize) : IRequest<PagedResult<AuditLogEntryDto>>;

public class GetClaimAuditLogQueryHandler(IClaimRepository claimRepository, IAuditLogService auditLogService, IUserDirectory userDirectory)
    : IRequestHandler<GetClaimAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetClaimAuditLogQuery request, CancellationToken cancellationToken)
    {
        _ = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var (items, totalCount) = await auditLogService.GetForClaimAsync(
            request.ClaimId, request.Page, request.PageSize, cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync(items.Select(a => a.CreatedByUserId), cancellationToken);
        var dtos = AuditLogEntryMapper.ToDtos(items, displayNames);

        return new PagedResult<AuditLogEntryDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
