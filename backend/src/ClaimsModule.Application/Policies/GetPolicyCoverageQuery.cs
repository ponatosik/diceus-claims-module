using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Policies;

/// <summary>GET /api/policies/{id}/coverage — the coverage types available on a seeded policy, used during FNOL (§10.4).</summary>
public record GetPolicyCoverageQuery(Guid PolicyId) : IRequest<PolicyCoverageDto>;

public record PolicyCoverageDto(Guid PolicyId, string PolicyNumber, List<string> CoverageTypes, DateOnly EffectiveDate, DateOnly ExpirationDate, string Status);

public class GetPolicyCoverageQueryHandler(IPolicyRepository repository) : IRequestHandler<GetPolicyCoverageQuery, PolicyCoverageDto>
{
    public async Task<PolicyCoverageDto> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(request.PolicyId, cancellationToken)
            ?? throw new NotFoundException("Policy", request.PolicyId);

        return new PolicyCoverageDto(
            policy.PolicyId,
            policy.PolicyNumber,
            policy.CoverageTypes.Split(',').ToList(),
            policy.EffectiveDate,
            policy.ExpirationDate,
            policy.Status.ToString());
    }
}
