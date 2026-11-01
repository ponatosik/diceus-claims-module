namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// A single non-field business-rule failure (closure conditions, invalid transitions, self-approval
/// rejection, etc.), per ADR-0008. <see cref="Code"/> uses the existing BR-*/CC-* rule ids from
/// REQUIREMENTS.md where one exists.
/// </summary>
public record BlockingCondition(string Code, string Message);
