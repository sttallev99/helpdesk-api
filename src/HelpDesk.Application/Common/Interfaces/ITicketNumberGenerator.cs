namespace HelpDesk.Application.Common.Interfaces;

/// <summary>
/// Generates a unique, human-friendly ticket number (e.g. "TCK-000123"). Backed by a
/// SQL Server sequence in Infrastructure so concurrent ticket creation never collides.
/// </summary>
public interface ITicketNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
