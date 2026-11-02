using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using MediatR;

namespace ClaimsModule.Application.Claims.OpenReserve;

public class OpenReserveCommandHandler(
    IClaimRepository claimRepository,
    IClaimReserveComponentRepository reserveComponentRepository,
    ReserveTransactionRecorder reserveTransactionRecorder,
    IUserDirectory userDirectory) : IRequestHandler<OpenReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(OpenReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim", request.ClaimId);
        ReserveClaimStatusGuard.EnsureMutable(claim);

        var (history, component) = await reserveTransactionRecorder.RecordAsync(
            claim, request.Component, request.Amount, request.ChangeReason, request.TransactionType, cancellationToken);

        await reserveComponentRepository.CommitChangesAsync(cancellationToken);

        var displayNames = await userDirectory.GetDisplayNamesAsync(ReserveTransactionMapper.UserIdsOf(history), cancellationToken);
        return ReserveTransactionMapper.ToDto(history, component.Component, displayNames);
    }
}
