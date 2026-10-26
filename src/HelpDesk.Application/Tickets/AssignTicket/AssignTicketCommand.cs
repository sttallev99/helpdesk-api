using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.AssignTicket;

public sealed record AssignTicketCommand(Guid TicketId, Guid AgentId) : IRequest<TicketDto>;

public sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(x => x.AgentId).NotEmpty();
    }
}

public sealed class AssignTicketCommandHandler : IRequestHandler<AssignTicketCommand, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserDirectory _userDirectory;

    public AssignTicketCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IUserDirectory userDirectory)
    {
        _context = context;
        _currentUser = currentUser;
        _userDirectory = userDirectory;
    }

    public async Task<TicketDto> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.History)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        if (!await _userDirectory.IsInRoleAsync(request.AgentId, Roles.Agent, cancellationToken))
        {
            throw new NotFoundException("Agent", request.AgentId);
        }

        ticket.Assign(request.AgentId, _currentUser.UserId!.Value);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("The ticket was modified by someone else. Reload and try again.", ex);
        }

        return TicketDto.FromEntity(ticket);
    }
}
