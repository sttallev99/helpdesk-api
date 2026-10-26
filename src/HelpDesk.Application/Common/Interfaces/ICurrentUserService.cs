namespace HelpDesk.Application.Common.Interfaces;

/// <summary>
/// Exposes the caller's identity as resolved from the validated JWT, so Application
/// handlers can enforce ownership/visibility rules without taking a dependency on
/// ASP.NET Core's HttpContext.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsInRole(string role);
}
