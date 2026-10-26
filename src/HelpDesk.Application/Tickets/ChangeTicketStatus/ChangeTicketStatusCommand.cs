using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(Guid TicketId, TicketStatus NewStatus) : IRequest<TicketDto>;

public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}

public sealed class ChangeTicketStatusCommandHandler : IRequestHandler<ChangeTicketStatusCommand, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ChangeTicketStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> Handle(ChangeTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.History)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        TicketAccessGuard.EnsureVisible(ticket, _currentUser);

        var isCustomer = _currentUser.IsInRole(Roles.Customer);

        if (isCustomer && request.NewStatus != TicketStatus.Open)
        {
            throw new ForbiddenAccessException("Customers may only reopen tickets, not set other statuses.");
        }

        ticket.ChangeStatus(request.NewStatus, _currentUser.UserId!.Value);

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
