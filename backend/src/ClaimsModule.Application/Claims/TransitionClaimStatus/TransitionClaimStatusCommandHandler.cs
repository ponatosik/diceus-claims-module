using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.TransitionClaimStatus;

public class TransitionClaimStatusCommandHandler(
    IClaimRepository claimRepository,
    ClaimTransitionEvaluator transitionEvaluator,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IMapper mapper) : IRequestHandler<TransitionClaimStatusCommand, ClaimDto>
{
    public async Task<ClaimDto> Handle(TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var evaluation = await transitionEvaluator.EvaluateAsync(
            claim, request.TargetStatus, currentUserService.Role, request.Reason, request.JustificationNote, cancellationToken);
        if (evaluation.BlockingConditions.Count > 0)
        {
            throw new BusinessRuleViolationException(evaluation.BlockingConditions);
        }

        switch (request.TargetStatus)
        {
            case ClaimStatus.Closed:
                await CloseAsync(claim, request, cancellationToken);
                return mapper.Map<ClaimDto>(claim);

            case ClaimStatus.Reopened:
                await ReopenAsync(claim, request, cancellationToken);
                return mapper.Map<ClaimDto>(claim);
        }

        var oldStatus = claim.Status;
        claim.Status = request.TargetStatus;
        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "STATUS_CHANGED", DescribeStatusChange(oldStatus, request.TargetStatus, request.Reason),
                oldStatus.ToString(), request.TargetStatus.ToString()),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);

        return mapper.Map<ClaimDto>(claim);
    }

    private static string DescribeStatusChange(ClaimStatus oldStatus, ClaimStatus newStatus, string? reason)
    {
        var description = $"Status changed from {oldStatus} to {newStatus}.";
        return string.IsNullOrWhiteSpace(reason) ? description : $"{description} Reason: {reason}";
    }

    /// <summary>CC-04's justification note is the entire point of the soft block — it must survive in the audit trail.</summary>
    private static string DescribeClosure(string claimNumber, string? justificationNote)
    {
        var description = $"Claim {claimNumber} closed.";
        return string.IsNullOrWhiteSpace(justificationNote) ? description : $"{description} Justification: {justificationNote}";
    }

    private async Task CloseAsync(Claim claim, TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var oldStatus = claim.Status;
        claim.Status = ClaimStatus.Closed;
        claim.ClosedAt = DateTimeOffset.UtcNow;
        claim.ClosureReason = request.Reason;

        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "STATUS_CHANGED", DescribeStatusChange(oldStatus, ClaimStatus.Closed, request.Reason),
                oldStatus.ToString(), ClaimStatus.Closed.ToString()),
            cancellationToken);
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_CLOSED", DescribeClosure(claim.ClaimNumber, request.JustificationNote), null, request.Reason),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Closed→Reopened cascades straight through to Open in one call (BR-ST-04: "the claim immediately
    /// transitions to Open after Reopened") — Reopened is never a durably persisted status.
    /// </summary>
    private async Task ReopenAsync(Claim claim, TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {ClaimStatus.Closed} to {ClaimStatus.Reopened}.", ClaimStatus.Closed.ToString(), ClaimStatus.Reopened.ToString()),
            cancellationToken);
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_REOPENED", $"Claim {claim.ClaimNumber} reopened.", null, request.Reason),
            cancellationToken);

        claim.Status = ClaimStatus.Open;

        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "STATUS_CHANGED", $"Status changed from {ClaimStatus.Reopened} to {ClaimStatus.Open}.", ClaimStatus.Reopened.ToString(), ClaimStatus.Open.ToString()),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
