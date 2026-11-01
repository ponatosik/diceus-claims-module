using Microsoft.AspNetCore.Http;

namespace ClaimsModule.API.Models;

/// <summary>Multipart form binding model for <c>POST /api/claims/{id}/documents</c> — API-layer only.</summary>
public class UploadClaimDocumentRequest
{
    public IFormFile File { get; set; } = null!;

    public string DocumentType { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
