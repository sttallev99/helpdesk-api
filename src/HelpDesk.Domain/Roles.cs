namespace HelpDesk.Domain;

/// <summary>
/// Canonical role names shared by Identity role seeding, authorization policies,
/// and JWT role claims. Keep this the single source of truth for role names.
/// </summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Agent = "Agent";
    public const string Admin = "Admin";

    public static readonly IReadOnlyCollection<string> All = new[] { Customer, Agent, Admin };
}
