using FluentValidation.Results;

namespace ClaimsModule.Application.Common;

/// <summary>
/// Implemented by a MediatR request that wants Severity.Warning validation failures attached to it
/// rather than silently dropped by <see cref="Behaviors.ValidationBehavior{TRequest,TResponse}"/>
/// (ADR-0008) — the handler, not the pipeline behavior, decides what to do with them (e.g. writing a
/// VALIDATION_ISSUE_ADDED audit entry once a ClaimId exists).
/// </summary>
public interface IHasValidationWarnings
{
    List<ValidationFailure> Warnings { get; }
}
