using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Application.Tickets;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Comments.AddInternalNote;

/// <summary>
/// Adds an agent/admin-only note. The Api layer restricts this endpoint to those
/// roles, and ListComments/GetTicket never surface IsInternal=true rows to customers.
/// </summary>
public sealed record AddInternalNoteCommand(Guid TicketId, string Content) : IRequest<TicketCommentDto>;

public sealed class AddInternalNoteCommandValidator : AbstractValidator<AddInternalNoteCommand>
{
    public AddInternalNoteCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(TicketComment.MaxContentLength);
    }
}

public sealed class AddInternalNoteCommandHandler : IRequestHandler<AddInternalNoteCommand, TicketCommentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddInternalNoteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketCommentDto> Handle(AddInternalNoteCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.History)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            throw new NotFoundException(nameof(Ticket), request.TicketId);
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new ForbiddenAccessException("Cannot add a note to a closed ticket.");
        }

        var comment = ticket.AddComment(_currentUser.UserId!.Value, request.Content, isInternal: true, isCustomerReply: false);

        await _context.SaveChangesAsync(cancellationToken);

        return new TicketCommentDto(comment.Id, comment.AuthorId, comment.Content, comment.IsInternal, comment.CreatedAt);
    }
}
