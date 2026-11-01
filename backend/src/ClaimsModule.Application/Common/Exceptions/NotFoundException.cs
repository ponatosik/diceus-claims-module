namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity doesn't exist; mapped to 404 by the API's exception-handling middleware (ADR-0006).</summary>
public class NotFoundException(string entityName, object id)
    : Exception($"{entityName} '{id}' was not found.");
