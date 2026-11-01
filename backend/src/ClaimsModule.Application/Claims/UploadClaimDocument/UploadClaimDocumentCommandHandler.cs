using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain;
using MediatR;

namespace ClaimsModule.Application.Claims.UploadClaimDocument;

public class UploadClaimDocumentCommandHandler(
    IClaimRepository claimRepository,
    IClaimDocumentRepository documentRepository,
    IStorageService storageService,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<UploadClaimDocumentCommand, ClaimDocumentDetailDto>
{
    public async Task<ClaimDocumentDetailDto> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var sanitisedFileName = DocumentFileNameSanitizer.Sanitize(request.FileName);
        var blobPath = await storageService.UploadAsync(
            claim.OrganisationId, claim.ClaimId, sanitisedFileName, request.Content, request.ContentType, cancellationToken);

        var document = new ClaimDocument
        {
            ClaimDocumentId = Guid.NewGuid(),
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

        return new ClaimDocumentDetailDto(
            document.ClaimDocumentId, document.ClaimId, document.DocumentType, document.DocumentName,
            document.ContentType, document.FileSizeBytes, document.UploadedAt, document.UploadedByUserId,
            document.Notes, downloadUrl);
    }
}
