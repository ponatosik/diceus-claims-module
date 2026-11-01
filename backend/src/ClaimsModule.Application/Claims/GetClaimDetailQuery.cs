using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims/{id} — full claim detail: parties, risk objects, reserve summary, documents, recent audit entries (§10.1).</summary>
public record GetClaimDetailQuery(Guid ClaimId) : IRequest<ClaimDetailDto>;

public class GetClaimDetailQueryHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    IClaimDocumentRepository documentRepository,
    IAuditLogService auditLogService) : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    private const int RecentAuditEntryCount = 5;

    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var reserves = await reserveComponentRepository.ListByClaimIdAsync(claim.ClaimId, cancellationToken);
        var documents = await documentRepository.ListByClaimIdAsync(claim.ClaimId, cancellationToken);
        var (recentAuditEntries, _) = await auditLogService.GetForClaimAsync(claim.ClaimId, 1, RecentAuditEntryCount, cancellationToken);

        return new ClaimDetailDto(
            claim.ClaimId,
            claim.ClaimNumber,
            claim.Status.ToString(),
            claim.PolicyId,
            claim.PolicyNumber,
            claim.ClientName,
            claim.ReportedDate,
            new LossEventDto(
                claim.LossEvent!.LossDate,
                claim.LossEvent.LossDescription,
                claim.LossEvent.LossLocation,
                claim.LossEvent.CauseOfLossCode,
                claim.LossEvent.EstimatedLossAmount),
            claim.Parties.Select(p => new ClaimPartyDto(
                p.ClaimPartyId, p.PartyRole.ToString(), p.PartyType.ToString(),
                p.FirstName, p.LastName, p.CompanyName, p.Email, p.Phone, p.IsActive)).ToList(),
            claim.RiskObjects.Select(r => new ClaimRiskObjectDto(
                r.ClaimRiskObjectId, r.AssetType.ToString(), r.AssetDescription,
                r.DamageDescription, r.IsPrimary, r.AssetReference)).ToList(),
            reserves.Select(r => new ReserveSummaryDto(
                r.ReserveComponentId, r.Component.ToString(), r.CurrentAmount, r.Status.ToString())).ToList(),
            documents.Select(d => new ClaimDocumentDto(
                d.ClaimDocumentId, d.DocumentName, d.DocumentType, d.UploadedAt)).ToList(),
            recentAuditEntries.Select(a => new AuditLogEntryDto(a.CreatedAt, a.EventType, a.Description)).ToList());
    }
}
