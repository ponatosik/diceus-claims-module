using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Append-only event log of reserve transactions (§9.6). Never updated or deleted — a correction is
/// always a new row. <see cref="IdempotencyKey"/> backs the GL posting job's idempotency guarantee
/// (ADR-0005) and carries a database-level unique constraint.
/// </summary>
public class ReserveHistory : AppendOnlyEntity
{
    public Guid ReserveHistoryId { get; set; }
    public Guid ReserveComponentId { get; set; }

    /// <summary>Denormalised for query convenience.</summary>
    public Guid ClaimId { get; set; }

    public ReserveTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal NewBalance { get; set; }
    public ReserveApprovalStatus ApprovalStatus { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public ReservePostingStatus PostingStatus { get; set; }
    public string? PostingJobId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int ChangeSequence { get; set; }
    public Guid? SubmittedByUserId { get; set; }

    /// <summary>
    /// Optimistic concurrency token. ReserveHistory rows are mutated in place on approve/reject/retract
    /// (Resolved Ambiguity 5 of the Slice C plan) rather than truly append-only for those transitions,
    /// so without this a second approve/reject/retract racing the first would silently last-write-win.
    /// </summary>
    public byte[] RowVer { get; set; } = [];
}
