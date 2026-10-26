namespace HelpDesk.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);

public sealed record MeResponse(Guid Id, string Email, string FullName, IReadOnlyCollection<string> Roles);
