namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// Thrown by a handler for a non-field business-rule violation (closure conditions, invalid status
/// transitions, self-approval rejection, etc.) — mapped to 422 with a <c>blockingConditions</c> array
/// by the API's exception-handling middleware (ADR-0006/ADR-0008), distinct from FluentValidation's
/// field-keyed <see cref="FluentValidation.ValidationException"/>.
/// </summary>
public class BusinessRuleViolationException(IReadOnlyList<BlockingCondition> blockingConditions) : Exception
{
    public IReadOnlyList<BlockingCondition> BlockingConditions { get; } = blockingConditions;
}
