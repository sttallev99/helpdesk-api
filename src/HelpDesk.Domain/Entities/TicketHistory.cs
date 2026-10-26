namespace HelpDesk.Domain.Entities;

/// <summary>
/// Immutable record of a single field change on a ticket. The current ticket state
/// answers "what is true now"; history answers "how did it get there".
/// </summary>
public class TicketHistory
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ChangedById { get; private set; }
    public string Field { get; private set; } = default!;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private TicketHistory()
    {
    }

    internal static TicketHistory Create(Guid ticketId, Guid changedById, string field, string? oldValue, string? newValue)
    {
        return new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedById = changedById,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTime.UtcNow
        };
    }
}
