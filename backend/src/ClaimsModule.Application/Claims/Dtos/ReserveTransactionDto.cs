namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>Shared response shape for every reserve-mutating command in Slice C (open, approve, reject, retract).</summary>
public record ReserveTransactionDto(
    Guid ReserveHistoryId,
    Guid ReserveComponentId,
    string Component,
    string TransactionType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    string ApprovalStatus,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    Guid? RejectedByUserId,
    DateTimeOffset? RejectedAt,
    string? RejectionReason,
    string ChangeReason,
    string PostingStatus,
    Guid? SubmittedByUserId,
    DateTimeOffset CreatedAt);
