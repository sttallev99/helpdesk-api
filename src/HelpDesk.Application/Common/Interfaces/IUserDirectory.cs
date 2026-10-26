namespace HelpDesk.Application.Common.Interfaces;

/// <summary>
/// Read/write access to Identity-managed accounts and role assignments, exposed to
/// the Application layer without a direct dependency on ASP.NET Core Identity.
/// </summary>
public interface IUserDirectory
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the user's current roles with the single given role.</summary>
    Task<bool> SetRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
}

public sealed record UserSummary(Guid Id, string Email, string FullName, IReadOnlyCollection<string> Roles);
