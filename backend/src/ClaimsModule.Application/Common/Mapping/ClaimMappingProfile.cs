using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain;

namespace ClaimsModule.Application.Common.Mapping;

public class ClaimMappingProfile : Profile
{
    public ClaimMappingProfile()
    {
        CreateMap<Claim, ClaimDto>();
        CreateMap<LossEvent, LossEventDto>();
        CreateMap<ClaimParty, ClaimPartyDto>();
        CreateMap<ClaimRiskObject, ClaimRiskObjectDto>();
    }
}
