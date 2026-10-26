using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Application.Tickets;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Comments.ListComments;

public sealed record ListCommentsQuery(Guid TicketId) : IRequest<IReadOnlyCollection<TicketCommentDto>>;

public sealed class ListCommentsQueryHandler : IRequestHandler<ListCommentsQuery, IReadOnlyCollection<TicketCommentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListCommentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>> Handle(ListCommentsQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        TicketAccessGuard.EnsureVisible(ticket, _currentUser);

        var canSeeInternal = _currentUser.IsInRole(Roles.Agent) || _currentUser.IsInRole(Roles.Admin);

        return await _context.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == request.TicketId && (canSeeInternal || !c.IsInternal))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TicketCommentDto(c.Id, c.AuthorId, c.Content, c.IsInternal, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
