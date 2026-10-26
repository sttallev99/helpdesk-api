using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.GetTicket;

public sealed record GetTicketQuery(Guid TicketId) : IRequest<TicketDto>;

public sealed class GetTicketQueryHandler : IRequestHandler<GetTicketQuery, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTicketQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> Handle(GetTicketQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        TicketAccessGuard.EnsureVisible(ticket, _currentUser);

        return TicketDto.FromEntity(ticket);
    }
}
