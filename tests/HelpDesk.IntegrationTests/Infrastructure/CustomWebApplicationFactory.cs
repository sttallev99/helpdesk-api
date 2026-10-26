using HelpDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HelpDesk.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real Api pipeline (controllers, auth, EF Core) against a throwaway
/// LocalDB database created fresh per test class and dropped on teardown. Validates
/// SQL Server-specific behavior (rowversion concurrency, unique indexes, the ticket
/// number sequence) instead of relying only on EF Core's in-memory provider.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"HelpDesk_IntegrationTests_{Guid.NewGuid():N}";

    public const string TestAdminEmail = "admin@helpdesk.test";
    public const string TestAdminPassword = "IntegrationTest123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting (not ConfigureAppConfiguration+AddInMemoryCollection) because Program.cs
        // reads Jwt:SigningKey eagerly while building the host - an in-memory config source
        // added via ConfigureAppConfiguration loses that race. UseSetting values are present
        // from the very first configuration read.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        builder.UseSetting("Jwt:Issuer", "HelpDesk.Tests");
        builder.UseSetting("Jwt:Audience", "HelpDesk.Tests.Client");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-needs-to-be-long-enough-for-hs256");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
        builder.UseSetting("SeedAdmin:Email", TestAdminEmail);
        builder.UseSetting("SeedAdmin:Password", TestAdminPassword);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Touching Services boots the host, which runs migrations + seeding (see Program.cs,
        // gated on IsDevelopment()) against the throwaway database above.
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.CanConnectAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
