using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Generic severity-aware validation pipeline behavior (ADR-0008). Blocks the request — throwing
/// FluentValidation's own <see cref="ValidationException"/> — only on <see cref="Severity.Error"/>
/// failures. <see cref="Severity.Warning"/> failures never block; they're attached to the request via
/// <see cref="IHasValidationWarnings"/> when it implements that marker, for the handler to act on
/// (e.g. auditing them) once persistence has happened.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).ToList();

            var errorFailures = failures.Where(f => f.Severity == Severity.Error).ToList();
            if (errorFailures.Count > 0)
            {
                throw new ValidationException(errorFailures);
            }

            var warningFailures = failures.Where(f => f.Severity == Severity.Warning).ToList();
            if (warningFailures.Count > 0 && request is IHasValidationWarnings warningAwareRequest)
            {
                warningAwareRequest.Warnings.AddRange(warningFailures);
            }
        }

        return await next(cancellationToken);
    }
}
