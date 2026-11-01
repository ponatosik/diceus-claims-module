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
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(100);

        // 200, not DocumentName's full 255-char column — the stored filename gets a "_{guid:N}"
        // uniqueness suffix (33 chars) appended after sanitising, and the result must still fit
        // comfortably under the local filesystem's 255-byte-per-segment limit (the tighter of the
        // two storage backends' constraints).
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage("File type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.");

        RuleFor(x => x.FileSizeBytes)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("File exceeds the 50MB size limit.");
    }
}
