namespace HelpDesk.Api.Contracts;

public sealed record CreateCategoryRequest(string Name);

public sealed record UpdateCategoryRequest(string Name, bool IsActive);

public sealed record ChangeUserRoleRequest(string Role);
