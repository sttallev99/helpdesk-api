using HelpDesk.Domain.Enums;

namespace HelpDesk.Domain.Exceptions;

public sealed class InvalidTicketTransitionException : DomainException
{
    public InvalidTicketTransitionException(TicketStatus from, TicketStatus to)
        : base($"Cannot transition ticket from '{from}' to '{to}'.")
    {
    }
}
