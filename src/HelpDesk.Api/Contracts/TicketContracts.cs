using HelpDesk.Domain.Enums;

namespace HelpDesk.Api.Contracts;

public sealed record CreateTicketRequest(string Title, string Description, TicketPriority Priority, Guid CategoryId);

public sealed record UpdateTicketRequest(string Title, string Description, Guid CategoryId);

public sealed record ChangeStatusRequest(TicketStatus NewStatus);

public sealed record AssignTicketRequest(Guid AgentId);

public sealed record AddCommentRequest(string Content);
