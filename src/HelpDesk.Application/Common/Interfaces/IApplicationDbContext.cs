using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Common.Interfaces;

/// <summary>
/// Persistence seam the Application layer depends on. Infrastructure provides the
/// EF Core implementation; Application never references EF Core or SQL Server directly.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketHistory> TicketHistories { get; }
    DbSet<Category> Categories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
