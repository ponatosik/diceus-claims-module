namespace ClaimsModule.Domain.Entities;

/// <summary>Metadata for a document attached to a claim; binary content lives in blob storage (§9.7).</summary>
public class ClaimDocument : AuditableEntity
{
    public Guid ClaimDocumentId { get; set; }
    public Guid ClaimId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? Notes { get; set; }
}
