using MediatR;

namespace ClaimsModule.Application.Claims.UpdateClaimNotes;

/// <summary>PATCH /api/claims/{id}/notes — §11.3 Overview's editable claim notes field (spec 006 story 9). Null or empty clears the notes.</summary>
public class UpdateClaimNotesCommand : IRequest
{
    public Guid ClaimId { get; set; }

    public string? Notes { get; set; }
}
