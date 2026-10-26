using Microsoft.AspNetCore.Identity;

namespace HelpDesk.Infrastructure.Identity;

/// <summary>
/// Identity-managed account. Credentials, password hashing, and lockout are handled
/// entirely by ASP.NET Core Identity; this type only adds the display name.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
}
