using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.GetTicketHistory;

public sealed record GetTicketHistoryQuery(Guid TicketId) : IRequest<IReadOnlyCollection<TicketHistoryDto>>;

public sealed class GetTicketHistoryQueryHandler : IRequestHandler<GetTicketHistoryQuery, IReadOnlyCollection<TicketHistoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetTicketHistoryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<TicketHistoryDto>> Handle(GetTicketHistoryQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        TicketAccessGuard.EnsureVisible(ticket, _currentUser);

        return await _context.TicketHistories.AsNoTracking()
            .Where(h => h.TicketId == request.TicketId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new TicketHistoryDto(h.Id, h.ChangedById, h.Field, h.OldValue, h.NewValue, h.ChangedAt))
            .ToListAsync(cancellationToken);
    }
}
