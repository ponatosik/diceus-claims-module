using ClaimsModule.Application.Common;
using MediatR;

namespace ClaimsModule.Application.Users;

/// <summary>Looks up one Demo User (GLOSSARY.md) for the dev token issuer (ADR-0010); null when the id isn't a seeded demo user.</summary>
public record GetDemoUserQuery(Guid UserId) : IRequest<DemoUserDto?>;

public record DemoUserDto(Guid UserId, string DisplayName, string Role);

public class GetDemoUserQueryHandler(IUserDirectory userDirectory) : IRequestHandler<GetDemoUserQuery, DemoUserDto?>
{
    public async Task<DemoUserDto?> Handle(GetDemoUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userDirectory.GetByIdAsync(request.UserId, cancellationToken);
        return user is null ? null : new DemoUserDto(user.UserId, user.DisplayName, user.Role);
    }
}
