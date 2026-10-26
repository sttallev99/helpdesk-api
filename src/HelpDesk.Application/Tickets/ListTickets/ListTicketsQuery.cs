using FluentValidation;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Application.Common.Models;
using HelpDesk.Domain;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Tickets.ListTickets;

public sealed record ListTicketsQuery(
    TicketStatus? Status = null,
    TicketPriority? Priority = null,
    Guid? CategoryId = null,
    bool? UnassignedOnly = null,
    bool? AssignedToMe = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PaginatedList<TicketSummaryDto>>;

public sealed class ListTicketsQueryValidator : AbstractValidator<ListTicketsQuery>
{
    public const int MaxPageSize = 100;

    public ListTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

public sealed class ListTicketsQueryHandler : IRequestHandler<ListTicketsQuery, PaginatedList<TicketSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListTicketsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public Task<PaginatedList<TicketSummaryDto>> Handle(ListTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsNoTracking().AsQueryable();

        if (_currentUser.IsInRole(Roles.Customer))
        {
            var customerId = _currentUser.UserId!.Value;
            query = query.Where(t => t.CustomerId == customerId);
        }

        if (request.Status is not null)
        {
            query = query.Where(t => t.Status == request.Status);
        }

        if (request.Priority is not null)
        {
            query = query.Where(t => t.Priority == request.Priority);
        }

        if (request.CategoryId is not null)
        {
            query = query.Where(t => t.CategoryId == request.CategoryId);
        }

        if (request.UnassignedOnly == true)
        {
            query = query.Where(t => t.AssignedAgentId == null);
        }

        if (request.AssignedToMe == true && _currentUser.UserId is not null)
        {
            var userId = _currentUser.UserId.Value;
            query = query.Where(t => t.AssignedAgentId == userId);
        }

        var projected = query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketSummaryDto(
                t.Id, t.TicketNumber, t.Title, t.Status.ToString(), t.Priority.ToString(),
                t.CategoryId, t.AssignedAgentId, t.CreatedAt, t.UpdatedAt));

        return PaginatedList<TicketSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
