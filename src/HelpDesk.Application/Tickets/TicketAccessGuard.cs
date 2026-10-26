using HelpDesk.Application.Common.Exceptions;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Domain.Entities;

namespace HelpDesk.Application.Tickets;

/// <summary>
/// Centralizes the "who can see this ticket" rule so every handler enforces it the
/// same way: customers never learn whether another customer's ticket exists.
/// </summary>
public static class TicketAccessGuard
{
    public static void EnsureVisible(Ticket ticket, ICurrentUserService currentUser)
    {
        if (currentUser.IsInRole(Roles.Customer) && ticket.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException(nameof(Ticket), ticket.Id);
        }
    }
}
