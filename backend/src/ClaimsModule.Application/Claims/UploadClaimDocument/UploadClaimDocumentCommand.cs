using ClaimsModule.Application.Claims.Dtos;
using MediatR;

namespace ClaimsModule.Application.Claims.UploadClaimDocument;

/// <summary>POST /api/claims/{id}/documents — uploads a supporting document to the claim (§10.1/§13).</summary>
public class UploadClaimDocumentCommand : IRequest<ClaimDocumentDetailDto>
{
    public Guid ClaimId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public Stream Content { get; set; } = Stream.Null;

    public string? Notes { get; set; }
}
