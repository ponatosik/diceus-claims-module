using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/claims/{id}/documents — lists a claim's documents with a download URL each (§10.1).</summary>
public record ListClaimDocumentsQuery(Guid ClaimId) : IRequest<List<ClaimDocumentDetailDto>>;

public class ListClaimDocumentsQueryHandler(
    IClaimRepository claimRepository,
    IClaimDocumentRepository documentRepository,
    IStorageService storageService) : IRequestHandler<ListClaimDocumentsQuery, List<ClaimDocumentDetailDto>>
{
    public async Task<List<ClaimDocumentDetailDto>> Handle(ListClaimDocumentsQuery request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);

        var documents = await documentRepository.ListByClaimIdAsync(claim.ClaimId, cancellationToken);

        var result = new List<ClaimDocumentDetailDto>();
        foreach (var document in documents)
        {
            var downloadUrl = await storageService.GetDownloadUrlAsync(document.BlobPath, cancellationToken);
            result.Add(new ClaimDocumentDetailDto(
                document.ClaimDocumentId, document.ClaimId, document.DocumentType, document.DocumentName,
                document.ContentType, document.FileSizeBytes, document.UploadedAt, document.UploadedByUserId,
                document.Notes, downloadUrl));
        }

        return result;
    }
}
