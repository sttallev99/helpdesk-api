using HelpDesk.Application.Common.Interfaces;
using MediatR;

namespace HelpDesk.Application.Users.ListUsers;

public sealed record ListUsersQuery : IRequest<IReadOnlyCollection<UserDto>>;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, IReadOnlyCollection<UserDto>>
{
    private readonly IUserDirectory _userDirectory;

    public ListUsersQueryHandler(IUserDirectory userDirectory)
    {
        _userDirectory = userDirectory;
    }

    public async Task<IReadOnlyCollection<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userDirectory.ListUsersAsync(cancellationToken);
        return users.Select(u => new UserDto(u.Id, u.Email, u.FullName, u.Roles)).ToList();
    }
}
