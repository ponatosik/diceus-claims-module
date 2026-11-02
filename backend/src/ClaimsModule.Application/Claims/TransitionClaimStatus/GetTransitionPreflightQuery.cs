using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.TransitionClaimStatus;

/// <summary>
/// GET /api/claims/{id}/status/preflight?target={status} — the Pre-flight Check (GLOSSARY.md) behind
/// §11.3's Closed-transition checklist (spec 006 story 14). A Query: evaluates, never changes or audits anything.
/// </summary>
public record GetTransitionPreflightQuery(Guid ClaimId, ClaimStatus TargetStatus) : IRequest<TransitionPreflightDto>;

/// <param name="BlockingConditions">Conditions that would block the transition no matter what the user enters in the confirmation dialog.</param>
/// <param name="Warnings">Non-blocking issues the dialog must surface — currently CC-04 (open reserves), which a justification note resolves.</param>
/// <param name="RequiresReason">The dialog must collect a reason (withdrawal, reopen).</param>
/// <param name="RequiresJustificationNote">The dialog must collect a CC-04 justification note.</param>
public record TransitionPreflightDto(
    bool IsTransitionValid,
    IReadOnlyList<BlockingCondition> BlockingConditions,
    IReadOnlyList<BlockingCondition> Warnings,
    bool RequiresReason,
    bool RequiresJustificationNote);

public class GetTransitionPreflightQueryHandler(
    IClaimRepository claimRepository,
    ClaimTransitionEvaluator transitionEvaluator,
    ICurrentUserService currentUserService) : IRequestHandler<GetTransitionPreflightQuery, TransitionPreflightDto>
{
    // The pre-flight answers "what blocks this regardless of what I type in the dialog?" — so inputs
    // the dialog itself collects (reason, CC-04 justification) are evaluated as if provided, and the
    // dialog is told to require them via the Requires* flags instead.
    private const string ProvidedInDialog = "(provided in the confirmation dialog)";

    public async Task<TransitionPreflightDto> Handle(GetTransitionPreflightQuery request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var evaluation = await transitionEvaluator.EvaluateAsync(
            claim, request.TargetStatus, currentUserService.Role, ProvidedInDialog, ProvidedInDialog, cancellationToken);

        var closingWithOpenReserves = request.TargetStatus == ClaimStatus.Closed && evaluation.HasOpenReserves;
        IReadOnlyList<BlockingCondition> warnings = closingWithOpenReserves
            ? [new BlockingCondition("CC-04", "This claim has open reserves — a justification note is required to close it anyway.")]
            : [];

        return new TransitionPreflightDto(
            evaluation.IsValidTransition,
            evaluation.BlockingConditions,
            warnings,
            RequiresReason: evaluation.IsValidTransition && request.TargetStatus is ClaimStatus.Withdrawn or ClaimStatus.Reopened,
            RequiresJustificationNote: closingWithOpenReserves);
    }
}
