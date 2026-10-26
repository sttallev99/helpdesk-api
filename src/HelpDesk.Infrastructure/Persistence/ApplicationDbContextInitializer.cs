using HelpDesk.Domain;
using HelpDesk.Domain.Entities;
using HelpDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HelpDesk.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds roles, starter categories, and a development
/// admin account. The admin password comes from configuration (user secrets / env
/// vars) via "SeedAdmin:Password" - never hardcoded, and only seeded if missing.
/// </summary>
public class ApplicationDbContextInitializer
{
    private readonly ILogger<ApplicationDbContextInitializer> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IConfiguration _configuration;

    public ApplicationDbContextInitializer(
        ILogger<ApplicationDbContextInitializer> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task MigrateAsync()
    {
        await _context.Database.MigrateAsync();
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedCategoriesAsync();
        await SeedAdminUserAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private async Task SeedCategoriesAsync()
    {
        if (_context.Categories.Any())
        {
            return;
        }

        var categories = new[] { "General", "Technical", "Billing", "Account" }
            .Select(Category.Create);

        _context.Categories.AddRange(categories);
        await _context.SaveChangesAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        var adminEmail = _configuration["SeedAdmin:Email"] ?? "admin@helpdesk.local";
        var adminPassword = _configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning(
                "SeedAdmin:Password is not configured; skipping admin account seeding. " +
                "Set it via user secrets or an environment variable to seed a dev admin.");
            return;
        }

        if (await _userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "HelpDesk Administrator",
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(admin, Roles.Admin);
        }
        else
        {
            _logger.LogError(
                "Failed to seed admin account: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
