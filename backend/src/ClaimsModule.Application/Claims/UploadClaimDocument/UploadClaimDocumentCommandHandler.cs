using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.UploadClaimDocument;

public class UploadClaimDocumentCommandHandler(
    IClaimRepository claimRepository,
    IClaimDocumentRepository documentRepository,
    IStorageService storageService,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    IUserDirectory userDirectory) : IRequestHandler<UploadClaimDocumentCommand, ClaimDocumentDetailDto>
{
    public async Task<ClaimDocumentDetailDto> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        // Generated before the upload so the stored filename can be made unique per document — two
        // uploads of the same original filename to the same claim must never collide on one storage
        // path and silently overwrite each other's bytes.
        var claimDocumentId = Guid.NewGuid();
        var uniqueFileName = MakeUnique(DocumentFileNameSanitizer.Sanitize(request.FileName), claimDocumentId);
        var blobPath = await storageService.UploadAsync(
            claim.OrganisationId, claim.ClaimId, uniqueFileName, request.Content, request.ContentType, cancellationToken);

        var document = new ClaimDocument
        {
            ClaimDocumentId = claimDocumentId,
            ClaimId = claim.ClaimId,
            DocumentType = request.DocumentType,
            DocumentName = request.FileName,
            BlobPath = blobPath,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedByUserId = currentUserService.UserId,
            Notes = request.Notes,
        };
        documentRepository.Add(document);

        await auditLogService.LogAsync(
            new AuditLogEntry(
                claim.ClaimId, "DOCUMENT_UPLOADED", $"Document uploaded: {request.FileName}.",
                null, null, document.ClaimDocumentId, "ClaimDocument"),
            cancellationToken);

        await documentRepository.CommitChangesAsync(cancellationToken);

        var downloadUrl = await storageService.GetDownloadUrlAsync(document.BlobPath, cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync([document.UploadedByUserId], cancellationToken);

        return new ClaimDocumentDetailDto(
            document.ClaimDocumentId, document.ClaimId, document.DocumentType, document.DocumentName,
            document.ContentType, document.FileSizeBytes, document.UploadedAt, document.UploadedByUserId,
            document.Notes, downloadUrl,
            document.UploadedByUserId is { } uploaderId ? displayNames.GetValueOrDefault(uploaderId) : null);
    }

    /// <summary>Inserts the document id before the extension, e.g. "report.pdf" → "report_{id:N}.pdf".</summary>
    private static string MakeUnique(string sanitisedFileName, Guid claimDocumentId)
    {
        var extension = Path.GetExtension(sanitisedFileName);
        var stem = Path.GetFileNameWithoutExtension(sanitisedFileName);
        return $"{stem}_{claimDocumentId:N}{extension}";
    }
}
