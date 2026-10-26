namespace HelpDesk.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AccessToken CreateToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
