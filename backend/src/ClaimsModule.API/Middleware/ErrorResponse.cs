using ClaimsModule.Application.Common.Exceptions;

namespace ClaimsModule.API.Middleware;

/// <summary>The §10.5 error body shape, extended per ADR-0008 with an optional <see cref="BlockingConditions"/> array.</summary>
public class ErrorResponse
{
    public required string Type { get; set; }

    public required string Title { get; set; }

    public required int Status { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }

    public IReadOnlyList<BlockingCondition>? BlockingConditions { get; set; }
}
