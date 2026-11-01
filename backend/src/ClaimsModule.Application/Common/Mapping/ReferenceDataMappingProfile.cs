using AutoMapper;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain;

namespace ClaimsModule.Application.Common.Mapping;

public class ReferenceDataMappingProfile : Profile
{
    public ReferenceDataMappingProfile()
    {
        CreateMap<CauseOfLossCode, CauseOfLossCodeDto>();

        CreateMap<Policy, PolicySearchResultDto>();
    }
}
