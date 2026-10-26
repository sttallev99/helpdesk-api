namespace HelpDesk.Domain.Exceptions;

public sealed class InvalidTicketOperationException : DomainException
{
    public InvalidTicketOperationException(string message) : base(message)
    {
    }
}
