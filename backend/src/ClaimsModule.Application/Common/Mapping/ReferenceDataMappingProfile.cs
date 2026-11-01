using AutoMapper;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Policies;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Mapping;

public class ReferenceDataMappingProfile : Profile
{
    public ReferenceDataMappingProfile()
    {
        CreateMap<CauseOfLossCode, CauseOfLossCodeDto>();

        CreateMap<Policy, PolicySearchResultDto>();
    }
}
