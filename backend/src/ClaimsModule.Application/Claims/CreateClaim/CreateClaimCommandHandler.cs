using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.CreateClaim;

public class CreateClaimCommandHandler(
    IClaimRepository claimRepository,
    IPolicyRepository policyRepository,
    IClaimNumberGenerator claimNumberGenerator,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IMapper mapper) : IRequestHandler<CreateClaimCommand, ClaimDto>
{
    public async Task<ClaimDto> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var claimNumber = await claimNumberGenerator.GenerateAsync(cancellationToken);
        var policy = request.PolicyId is { } policyId
            ? await policyRepository.GetByIdAsync(policyId, cancellationToken)
            : null;

        // ClaimId is assigned client-side (not left to NEWSEQUENTIALID()) so the audit entries below
        // can reference the real id in the same SaveChangesAsync call as the claim+children insert —
        // see the plan's Resolved Ambiguity 4. OrganisationId/CreatedAt are NOT set here; the
        // AuditableEntitySaveChangesInterceptor stamps both automatically on insert.
        var claim = new Claim
        {
            ClaimId = Guid.NewGuid(),
            ClaimNumber = claimNumber,
            // policy?.PolicyId, not request.PolicyId: an id that doesn't resolve is only a Warning
            // (not a block), but Claims.PolicyId is a real FK (ClaimConfiguration) — persisting the
            // caller's unresolved id would throw DbUpdateException (-> 500) on SaveChanges.
            PolicyId = policy?.PolicyId,
            PolicyNumber = policy?.PolicyNumber,
            ClientName = policy?.ClientName,
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
            AssignedHandlerId = currentUserService.UserId,
            LossEvent = new LossEvent
            {
                LossDate = request.LossDate,
                LossDescription = request.LossDescription,
                LossLocation = request.LossLocation,
                CauseOfLossCode = request.CauseOfLossCode,
                EstimatedLossAmount = request.EstimatedLossAmount,
                ReportDate = DateTimeOffset.UtcNow,
            },
            Parties = [.. (request.Parties ?? []).Select(p => new ClaimParty
            {
                PartyRole = p.PartyRole,
                PartyType = p.PartyType,
                FirstName = p.FirstName,
                LastName = p.LastName,
                CompanyName = p.CompanyName,
                Email = p.Email,
                Phone = p.Phone,
            })],
            RiskObjects = [.. (request.RiskObjects ?? []).Select(r => new ClaimRiskObject
            {
                AssetType = r.AssetType,
                AssetDescription = r.AssetDescription,
                DamageDescription = r.DamageDescription,
                IsPrimary = r.IsPrimary,
                AssetReference = r.AssetReference,
            })],
        };

        claimRepository.Add(claim);

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_CREATED", $"Claim {claim.ClaimNumber} created."), cancellationToken);

        foreach (var warning in request.Warnings)
        {
            await auditLogService.LogAsync(
                new AuditLogEntry(claim.ClaimId, "VALIDATION_ISSUE_ADDED", warning.ErrorMessage), cancellationToken);
        }

        await claimRepository.CommitChangesAsync(cancellationToken);

        return mapper.Map<ClaimDto>(claim);
    }
}
