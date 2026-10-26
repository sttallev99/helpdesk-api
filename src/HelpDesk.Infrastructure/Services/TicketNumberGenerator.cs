using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

/// <summary>
/// Formats ticket numbers as "TCK-000123" using a SQL Server sequence, so concurrent
/// ticket creation across requests never produces a duplicate number. Uses a raw
/// ADO.NET command (not EF's SqlQueryRaw) because SQL Server disallows "NEXT VALUE FOR"
/// inside the subquery EF wraps scalar queries in.
/// </summary>
public class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly ApplicationDbContext _context;

    public TicketNumberGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT NEXT VALUE FOR [dbo].[{ApplicationDbContext.TicketNumberSequenceName}]";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var next = Convert.ToInt64(result);

            return $"TCK-{next:D6}";
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
}
