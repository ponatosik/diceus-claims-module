using AutoMapper;
using MediatR;

namespace ClaimsModule.Application.Policies;

/// <summary>GET /api/policies/search — searches the seeded simulated policy table by number or client name (§5.5/§10.4).</summary>
public record SearchPoliciesQuery(string Query) : IRequest<List<PolicySearchResultDto>>;

public record PolicySearchResultDto(Guid PolicyId, string PolicyNumber, string ClientName, DateOnly EffectiveDate, DateOnly ExpirationDate, string Status);

public class SearchPoliciesQueryHandler(IPolicyRepository repository, IMapper mapper)
    : IRequestHandler<SearchPoliciesQuery, List<PolicySearchResultDto>>
{
    public async Task<List<PolicySearchResultDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await repository.SearchAsync(request.Query, cancellationToken);
        return mapper.Map<List<PolicySearchResultDto>>(policies);
    }
}
