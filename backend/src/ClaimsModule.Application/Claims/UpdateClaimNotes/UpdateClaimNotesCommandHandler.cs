using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims.UpdateClaimNotes;

public class UpdateClaimNotesCommandHandler(
    IClaimRepository claimRepository,
    IAuditLogService auditLogService) : IRequestHandler<UpdateClaimNotesCommand>
{
    public async Task Handle(UpdateClaimNotesCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var newNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes;
        if (newNotes == claim.Notes)
        {
            return;
        }

        var oldNotes = claim.Notes;
        claim.Notes = newNotes;

        // Not in §14.1's event list — added for traceability, same judgment call as RESERVE_OVERRIDE_SET.
        await auditLogService.LogAsync(
            new AuditLogEntry(claim.ClaimId, "CLAIM_NOTES_UPDATED", "Claim notes updated.", oldNotes, newNotes),
            cancellationToken);

        await claimRepository.CommitChangesAsync(cancellationToken);
    }
}
