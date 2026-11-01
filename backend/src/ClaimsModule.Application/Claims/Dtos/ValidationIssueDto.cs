namespace ClaimsModule.Application.Claims.Dtos;

public record ValidationIssueDto(string PropertyName, string Message, string Severity);
