namespace HelpDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown when the caller is authenticated and the resource is visible to them, but
/// they lack permission to perform the requested operation. Maps to 403.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
