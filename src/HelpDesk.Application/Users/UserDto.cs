namespace HelpDesk.Application.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, IReadOnlyCollection<string> Roles);
