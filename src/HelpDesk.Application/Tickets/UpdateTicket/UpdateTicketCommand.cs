using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace HelpDesk.Application.Tickets.UpdateTicket;

/// <summary>
/// Edits the mutable ticket details (title/description/category). Priority changes
/// and status transitions go through their own commands since they carry different
/// authorization and history semantics.
/// </summary>
public sealed record UpdateTicketCommand(
    Guid TicketId,
    string Title,
    string Description,
    Guid CategoryId) : IRequest<TicketDto>;

public sealed class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Ticket.MaxTitleLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Ticket.MaxDescriptionLength);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public sealed class UpdateTicketCommandHandler : IRequestHandler<UpdateTicketCommand, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateTicketCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketDto> Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
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

        var isOwner = ticket.CustomerId == _currentUser.UserId;
        var isStaff = _currentUser.IsInRole(Roles.Agent) || _currentUser.IsInRole(Roles.Admin);

        if (!isOwner && !isStaff)
        {
            throw new ForbiddenAccessException("You do not have permission to edit this ticket.");
        }

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), request.CategoryId);
        }

        ticket.UpdateDetails(request.Title, request.Description, request.CategoryId, _currentUser.UserId!.Value);

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
