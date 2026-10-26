namespace HelpDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown when a save fails because another request modified the same row first
/// (SQL Server rowversion mismatch). Maps to 409 Conflict - the caller should reload
/// and retry. This is how "two agents claim the same ticket" resolves to one winner.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
