using FluentValidation;
using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Application.Tickets;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Comments.AddComment;

/// <summary>Adds a public reply, visible to both the customer and staff.</summary>
public sealed record AddCommentCommand(Guid TicketId, string Content) : IRequest<TicketCommentDto>;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(TicketComment.MaxContentLength);
    }
}

public sealed class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, TicketCommentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddCommentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TicketCommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken)
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

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new ForbiddenAccessException("Cannot reply to a closed ticket.");
        }

        var isCustomerReply = _currentUser.IsInRole(Roles.Customer);
        var comment = ticket.AddComment(_currentUser.UserId!.Value, request.Content, isInternal: false, isCustomerReply: isCustomerReply);

        await _context.SaveChangesAsync(cancellationToken);

        return new TicketCommentDto(comment.Id, comment.AuthorId, comment.Content, comment.IsInternal, comment.CreatedAt);
    }
}
