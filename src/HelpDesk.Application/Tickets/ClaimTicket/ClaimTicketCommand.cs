using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.ClaimTicket;

/// <summary>
/// An agent claims an unassigned, open ticket. If two agents claim the same ticket at
/// the same time, the RowVersion concurrency token ensures only the first SaveChanges
/// wins; the second gets a ConcurrencyException (409) and must retry.
/// </summary>
public sealed record ClaimTicketCommand(Guid TicketId) : IRequest<TicketDto>;

public sealed class ClaimTicketCommandHandler : IRequestHandler<ClaimTicketCommand, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClaimTicketCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> Handle(ClaimTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.History)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        ticket.Claim(_currentUser.UserId!.Value);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("This ticket was just claimed by another agent.", ex);
        }

        return TicketDto.FromEntity(ticket);
    }
}
