using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using MediatR;

namespace HelpDesk.Application.Users.ChangeUserRole;

public sealed record ChangeUserRoleCommand(Guid UserId, string Role) : IRequest;

public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
    }
}

public sealed class ChangeUserRoleCommandHandler : IRequestHandler<ChangeUserRoleCommand>
{
    private readonly IUserDirectory _userDirectory;

    public ChangeUserRoleCommandHandler(IUserDirectory userDirectory)
    {
        _userDirectory = userDirectory;
    }

    public async Task Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!await _userDirectory.ExistsAsync(request.UserId, cancellationToken))
        {
            throw new NotFoundException("User", request.UserId);
        }

        await _userDirectory.SetRoleAsync(request.UserId, request.Role, cancellationToken);
    }
}
