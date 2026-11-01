namespace ClaimsModule.Application.Claims.Dtos;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
