namespace HelpDesk.Domain.Entities;

/// <summary>
/// A reply on a ticket. Public replies are visible to the customer; internal notes
/// (IsInternal = true) are agent/admin-only and must never be exposed to the customer
/// by the Api layer.
/// </summary>
public class TicketComment
{
    public const int MaxContentLength = 4000;

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = default!;
    public bool IsInternal { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TicketComment()
    {
    }

    internal static TicketComment Create(Guid ticketId, Guid authorId, string content, bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Comment content cannot be empty.", nameof(content));
        }

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException($"Comment content cannot exceed {MaxContentLength} characters.", nameof(content));
        }

        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            Content = content,
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow
        };
    }
}
