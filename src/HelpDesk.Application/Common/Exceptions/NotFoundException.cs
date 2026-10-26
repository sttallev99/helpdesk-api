namespace HelpDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown when a resource does not exist, or exists but should not be revealed to the
/// caller (e.g. a customer requesting another customer's ticket). Maps to 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"Entity \"{entityName}\" ({key}) was not found.")
    {
    }
}
