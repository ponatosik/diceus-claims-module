using ClaimsModule.Application.Common;
using AutoMapper;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims;

/// <summary>GET /api/reference/cause-of-loss-codes — lists active cause-of-loss reference data (§5.6), optionally filtered by peril category.</summary>
public record ListCauseOfLossCodesQuery(PerilCategory? PerilCategory) : IRequest<List<CauseOfLossCodeDto>>;

public record CauseOfLossCodeDto(string Code, string Name, string PerilCategory, int SortOrder);

public class ListCauseOfLossCodesQueryHandler(ICauseOfLossCodeRepository repository, IMapper mapper)
    : IRequestHandler<ListCauseOfLossCodesQuery, List<CauseOfLossCodeDto>>
{
    public async Task<List<CauseOfLossCodeDto>> Handle(ListCauseOfLossCodesQuery request, CancellationToken cancellationToken)
    {
        var codes = await repository.ListActiveAsync(request.PerilCategory, cancellationToken);
        return mapper.Map<List<CauseOfLossCodeDto>>(codes);
    }
}
