using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.CreateTicket;

public sealed record CreateTicketCommand(
    string Title,
    string Description,
    TicketPriority Priority,
    Guid CategoryId) : IRequest<TicketDto>;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Ticket.MaxTitleLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Ticket.MaxDescriptionLength);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public sealed class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, TicketDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITicketNumberGenerator _ticketNumberGenerator;

    public CreateTicketCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ITicketNumberGenerator ticketNumberGenerator)
    {
        _context = context;
        _currentUser = currentUser;
        _ticketNumberGenerator = ticketNumberGenerator;
    }

    public async Task<TicketDto> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId!.Value;

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), request.CategoryId);
        }

        var ticketNumber = await _ticketNumberGenerator.NextAsync(cancellationToken);

        var ticket = Ticket.Create(ticketNumber, request.Title, request.Description, request.Priority, request.CategoryId, customerId);

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        return TicketDto.FromEntity(ticket);
    }
}
