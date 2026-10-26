using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Exceptions;

namespace HelpDesk.Domain.Entities;

/// <summary>
/// Aggregate root for the support ticket lifecycle. All status/assignment changes
/// must go through this type so that a history entry is always recorded alongside
/// the change (see Application layer: Infrastructure persists both in one transaction).
/// </summary>
public class Ticket
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 8000;

    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> AllowedTransitions =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] = new[] { TicketStatus.InProgress, TicketStatus.WaitingForCustomer, TicketStatus.Resolved, TicketStatus.Closed },
            [TicketStatus.InProgress] = new[] { TicketStatus.WaitingForCustomer, TicketStatus.Resolved, TicketStatus.Open, TicketStatus.Closed },
            [TicketStatus.WaitingForCustomer] = new[] { TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Closed },
            [TicketStatus.Resolved] = new[] { TicketStatus.Closed, TicketStatus.Open },
            [TicketStatus.Closed] = new[] { TicketStatus.Open }
        };

    private readonly List<TicketComment> _comments = new();
    private readonly List<TicketHistory> _history = new();

    public Guid Id { get; private set; }
    public string TicketNumber { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>SQL Server rowversion token used for optimistic concurrency.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<TicketComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<TicketHistory> History => _history.AsReadOnly();

    private Ticket()
    {
    }

    public static Ticket Create(
        string ticketNumber,
        string title,
        string description,
        TicketPriority priority,
        Guid categoryId,
        Guid customerId)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new ArgumentException("Ticket number cannot be empty.", nameof(ticketNumber));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title must be between 1 and {MaxTitleLength} characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be between 1 and {MaxDescriptionLength} characters.", nameof(description));
        }

        var now = DateTime.UtcNow;

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TicketNumber = ticketNumber,
            Title = title,
            Description = description,
            Priority = priority,
            CategoryId = categoryId,
            CustomerId = customerId,
            Status = TicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };

        ticket._history.Add(TicketHistory.Create(ticket.Id, customerId, nameof(Status), null, TicketStatus.Open.ToString()));

        return ticket;
    }

    public bool CanTransitionTo(TicketStatus newStatus) =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(newStatus);

    /// <summary>Agent claims an unassigned, open ticket. Moves it to InProgress.</summary>
    public void Claim(Guid agentId)
    {
        if (AssignedAgentId is not null)
        {
            throw new InvalidTicketOperationException("Ticket is already assigned to an agent.");
        }

        if (Status != TicketStatus.Open)
        {
            throw new InvalidTicketOperationException("Only open tickets can be claimed.");
        }

        AssignedAgentId = agentId;
        ChangeStatusCore(TicketStatus.InProgress, agentId);
        AddHistoryEntry(agentId, nameof(AssignedAgentId), null, agentId.ToString());
    }

    /// <summary>Admin (or agent reassigning) explicitly assigns a ticket to an agent.</summary>
    public void Assign(Guid agentId, Guid changedById)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new InvalidTicketOperationException("Cannot assign a closed ticket.");
        }

        var previousAgentId = AssignedAgentId;
        AssignedAgentId = agentId;
        Touch();
        AddHistoryEntry(changedById, nameof(AssignedAgentId), previousAgentId?.ToString(), agentId.ToString());
    }

    public void ChangeStatus(TicketStatus newStatus, Guid changedById)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidTicketTransitionException(Status, newStatus);
        }

        ChangeStatusCore(newStatus, changedById);
    }

    /// <summary>Edits the title/description/category. Only legal while the ticket is open.</summary>
    public void UpdateDetails(string title, string description, Guid categoryId, Guid changedById)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new InvalidTicketOperationException("Cannot edit a resolved or closed ticket.");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title must be between 1 and {MaxTitleLength} characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be between 1 and {MaxDescriptionLength} characters.", nameof(description));
        }

        if (!string.Equals(Title, title, StringComparison.Ordinal))
        {
            AddHistoryEntry(changedById, nameof(Title), Title, title);
            Title = title;
        }

        if (!string.Equals(Description, description, StringComparison.Ordinal))
        {
            AddHistoryEntry(changedById, nameof(Description), Description, description);
            Description = description;
        }

        if (CategoryId != categoryId)
        {
            AddHistoryEntry(changedById, nameof(CategoryId), CategoryId.ToString(), categoryId.ToString());
            CategoryId = categoryId;
        }

        Touch();
    }

    public void ChangePriority(TicketPriority newPriority, Guid changedById)
    {
        if (Priority == newPriority)
        {
            return;
        }

        var old = Priority;
        Priority = newPriority;
        Touch();
        AddHistoryEntry(changedById, nameof(Priority), old.ToString(), newPriority.ToString());
    }

    /// <summary>A customer reply while the agent is waiting on them returns the ticket to Open.</summary>
    public TicketComment AddComment(Guid authorId, string content, bool isInternal, bool isCustomerReply)
    {
        var comment = TicketComment.Create(Id, authorId, content, isInternal);
        _comments.Add(comment);
        Touch();

        if (isCustomerReply && Status == TicketStatus.WaitingForCustomer)
        {
            ChangeStatusCore(TicketStatus.Open, authorId);
        }

        return comment;
    }

    private void ChangeStatusCore(TicketStatus newStatus, Guid changedById)
    {
        var old = Status;
        Status = newStatus;
        Touch();
        AddHistoryEntry(changedById, nameof(Status), old.ToString(), newStatus.ToString());
    }

    private void AddHistoryEntry(Guid changedById, string field, string? oldValue, string? newValue)
    {
        _history.Add(TicketHistory.Create(Id, changedById, field, oldValue, newValue));
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
