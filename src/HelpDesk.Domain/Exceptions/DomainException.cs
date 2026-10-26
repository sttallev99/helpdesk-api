namespace HelpDesk.Domain.Exceptions;

/// <summary>
/// Base type for violations of a domain invariant or business rule.
/// The Api layer maps these to 409 Conflict.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
