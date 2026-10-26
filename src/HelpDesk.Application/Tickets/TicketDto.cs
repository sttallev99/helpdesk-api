using HelpDesk.Domain.Entities;

namespace HelpDesk.Application.Tickets;

public sealed record TicketDto(
    Guid Id,
    string TicketNumber,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid CategoryId,
    Guid CustomerId,
    Guid? AssignedAgentId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TicketDto FromEntity(Ticket ticket) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.Title,
        ticket.Description,
        ticket.Status.ToString(),
        ticket.Priority.ToString(),
        ticket.CategoryId,
        ticket.CustomerId,
        ticket.AssignedAgentId,
        ticket.CreatedAt,
        ticket.UpdatedAt);
}

public sealed record TicketSummaryDto(
    Guid Id,
    string TicketNumber,
    string Title,
    string Status,
    string Priority,
    Guid CategoryId,
    Guid? AssignedAgentId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TicketHistoryDto(
    Guid Id,
    Guid ChangedById,
    string Field,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt);

public sealed record TicketCommentDto(
    Guid Id,
    Guid AuthorId,
    string Content,
    bool IsInternal,
    DateTime CreatedAt);
