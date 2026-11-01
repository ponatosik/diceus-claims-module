using FluentValidation;

namespace ClaimsModule.Application.Claims.UploadClaimDocument;

/// <summary>§13's MIME-type allowlist and 50MB size limit.</summary>
public class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv",
    };

    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty();

        RuleFor(x => x.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage("File type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.");

        RuleFor(x => x.FileSizeBytes)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("File exceeds the 50MB size limit.");
    }
}
