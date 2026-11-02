using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

/// <summary>
/// Shared hand-written mapping for every reserve-mutating command's response (open, approve, reject,
/// retract) — needs both the ReserveHistory row and its owning component's type, since the component
/// type itself (Indemnity/Expense/...) lives on ClaimReserveComponent, not ReserveHistory.
/// </summary>
public static class ReserveTransactionMapper
{
    /// <summary>The user ids a transaction references — resolve them in one <see cref="Common.IUserDirectory"/> call per response.</summary>
    public static IEnumerable<Guid?> UserIdsOf(ReserveHistory history) =>
        [history.SubmittedByUserId, history.ApprovedByUserId, history.RejectedByUserId];

    public static ReserveTransactionDto ToDto(
        ReserveHistory history, ReserveComponentType component, IReadOnlyDictionary<Guid, string> displayNames) => new(
        history.ReserveHistoryId,
        history.ReserveComponentId,
        component.ToString(),
        history.TransactionType.ToString(),
        history.Amount,
        history.PreviousBalance,
        history.NewBalance,
        history.ApprovalStatus.ToString(),
        history.ApprovedByUserId,
        history.ApprovedAt,
        history.RejectedByUserId,
        history.RejectedAt,
        history.RejectionReason,
        history.ChangeReason,
        history.PostingStatus.ToString(),
        history.SubmittedByUserId,
        history.CreatedAt,
        NameOf(history.SubmittedByUserId, displayNames),
        NameOf(history.ApprovedByUserId, displayNames),
        NameOf(history.RejectedByUserId, displayNames));

    private static string? NameOf(Guid? userId, IReadOnlyDictionary<Guid, string> displayNames) =>
        userId is { } id ? displayNames.GetValueOrDefault(id) : null;
}
