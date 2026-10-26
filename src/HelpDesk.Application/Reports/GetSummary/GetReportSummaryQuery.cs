using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Application.Reports.GetSummary;

public sealed record GetReportSummaryQuery : IRequest<ReportSummaryDto>;

public sealed record ReportSummaryDto(
    IReadOnlyDictionary<string, int> TicketsByStatus,
    int UnassignedOpenTickets,
    int OverdueTickets,
    double AverageResolutionHours,
    IReadOnlyDictionary<Guid, int> TicketsPerAgent);

public sealed class GetReportSummaryQueryHandler : IRequestHandler<GetReportSummaryQuery, ReportSummaryDto>
{
    /// <summary>A ticket not yet resolved/closed within this many hours of creation counts as overdue.</summary>
    private const int OverdueThresholdHours = 72;

    private readonly IApplicationDbContext _context;

    public GetReportSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportSummaryDto> Handle(GetReportSummaryQuery request, CancellationToken cancellationToken)
    {
        var statusCounts = await _context.Tickets.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, cancellationToken);

        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            statusCounts.TryAdd(status.ToString(), 0);
        }

        var unassignedOpen = await _context.Tickets.AsNoTracking()
            .CountAsync(t => t.AssignedAgentId == null && t.Status != TicketStatus.Closed, cancellationToken);

        var overdueThreshold = DateTime.UtcNow.AddHours(-OverdueThresholdHours);
        var overdueCount = await _context.Tickets.AsNoTracking()
            .CountAsync(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.CreatedAt < overdueThreshold, cancellationToken);

        var ticketsPerAgent = await _context.Tickets.AsNoTracking()
            .Where(t => t.AssignedAgentId != null)
            .GroupBy(t => t.AssignedAgentId!.Value)
            .Select(g => new { AgentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, cancellationToken);

        var averageResolutionHours = await CalculateAverageResolutionHoursAsync(cancellationToken);

        return new ReportSummaryDto(statusCounts, unassignedOpen, overdueCount, averageResolutionHours, ticketsPerAgent);
    }

    private async Task<double> CalculateAverageResolutionHoursAsync(CancellationToken cancellationToken)
    {
        var firstResolvedAt = await _context.TicketHistories.AsNoTracking()
            .Where(h => h.Field == nameof(Ticket.Status) && h.NewValue == TicketStatus.Resolved.ToString())
            .GroupBy(h => h.TicketId)
            .Select(g => new { TicketId = g.Key, ResolvedAt = g.Min(h => h.ChangedAt) })
            .ToListAsync(cancellationToken);

        if (firstResolvedAt.Count == 0)
        {
            return 0;
        }

        var ticketIds = firstResolvedAt.Select(r => r.TicketId).ToList();

        var createdAtByTicket = await _context.Tickets.AsNoTracking()
            .Where(t => ticketIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.CreatedAt, cancellationToken);

        var resolutionHours = firstResolvedAt
            .Where(r => createdAtByTicket.ContainsKey(r.TicketId))
            .Select(r => (r.ResolvedAt - createdAtByTicket[r.TicketId]).TotalHours)
            .ToList();

        return resolutionHours.Count > 0 ? resolutionHours.Average() : 0;
    }
}
