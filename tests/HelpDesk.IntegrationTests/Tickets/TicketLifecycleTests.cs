using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HelpDesk.IntegrationTests.Infrastructure;
using Xunit;

namespace HelpDesk.IntegrationTests.Tickets;

public class TicketLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TicketLifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private ApiClient NewClient() => new(_factory.CreateClient());

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}@test.com";

    private async Task<Guid> GetAnyCategoryIdAsync(ApiClient client)
    {
        var categories = await client.GetAsync<JsonElement>("/api/v1/categories");
        return Guid.Parse(categories[0].GetProperty("id").GetString()!);
    }

    private async Task<ApiClient> CreateAgentAsync()
    {
        var registerClient = NewClient();
        var email = Unique("agent");
        var (_, agentId) = await registerClient.RegisterAsync(email, "Agent12345!", "Test Agent");

        var adminClient = NewClient();
        var adminToken = await adminClient.LoginAsync(CustomWebApplicationFactory.TestAdminEmail, CustomWebApplicationFactory.TestAdminPassword);
        adminClient.AuthenticateAs(adminToken);
        await adminClient.PatchRawAsync($"/api/v1/admin/users/{agentId}/role", new { role = "Agent" });

        var agentClient = NewClient();
        var agentToken = await agentClient.LoginAsync(email, "Agent12345!");
        agentClient.AuthenticateAs(agentToken);
        return agentClient;
    }

    private async Task<(ApiClient Client, Guid TicketId)> CreateTicketAsCustomerAsync()
    {
        var customerClient = NewClient();
        await customerClient.RegisterAsync(Unique("customer"), "Customer123!", "Test Customer");

        var categoryId = await GetAnyCategoryIdAsync(customerClient);
        var ticket = await customerClient.PostAsync<JsonElement>("/api/v1/tickets", new
        {
            title = "Printer on fire",
            description = "Smoke from the office printer, please help.",
            priority = "High",
            categoryId
        });

        return (customerClient, Guid.Parse(ticket.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task CreateTicket_AsCustomer_ReturnsCreatedAndIsRetrievable()
    {
        var customerClient = NewClient();
        await customerClient.RegisterAsync(Unique("customer"), "Customer123!", "Test Customer");
        var categoryId = await GetAnyCategoryIdAsync(customerClient);

        var createResponse = await customerClient.PostRawAsync("/api/v1/tickets", new
        {
            title = "Keyboard not working",
            description = "No keys respond after the spill.",
            priority = "Normal",
            categoryId
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = created.GetProperty("id").GetString();

        var getResponse = await customerClient.GetRawAsync($"/api/v1/tickets/{ticketId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTicket_AsOtherCustomer_ReturnsNotFound()
    {
        var (_, ticketId) = await CreateTicketAsCustomerAsync();

        var otherCustomer = NewClient();
        await otherCustomer.RegisterAsync(Unique("other"), "Other12345!", "Another Customer");

        var response = await otherCustomer.GetRawAsync($"/api/v1/tickets/{ticketId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InternalNote_IsNeverVisibleToTheOwningCustomer()
    {
        var (customerClient, ticketId) = await CreateTicketAsCustomerAsync();
        var agentClient = await CreateAgentAsync();

        await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/claim", new { });
        var noteResponse = await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/internal-notes", new { content = "Escalating to facilities." });
        noteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var customerComments = await customerClient.GetAsync<JsonElement>($"/api/v1/tickets/{ticketId}/comments");
        customerComments.GetArrayLength().Should().Be(0);

        var agentComments = await agentClient.GetAsync<JsonElement>($"/api/v1/tickets/{ticketId}/comments");
        agentComments.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task CustomerCannotAddInternalNote()
    {
        var (customerClient, ticketId) = await CreateTicketAsCustomerAsync();

        var response = await customerClient.PostRawAsync($"/api/v1/tickets/{ticketId}/internal-notes", new { content = "sneaky" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeStatus_IllegalTransition_ReturnsConflict()
    {
        var (_, ticketId) = await CreateTicketAsCustomerAsync();
        var agentClient = await CreateAgentAsync();

        await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/claim", new { });
        await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/status", new { newStatus = "Resolved" });
        await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/status", new { newStatus = "Closed" });

        // Closed tickets may only go back to Open, not straight to InProgress.
        var response = await agentClient.PostRawAsync($"/api/v1/tickets/{ticketId}/status", new { newStatus = "InProgress" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_CanOnlyReopenNotSetArbitraryStatus()
    {
        var (customerClient, ticketId) = await CreateTicketAsCustomerAsync();

        var response = await customerClient.PostRawAsync($"/api/v1/tickets/{ticketId}/status", new { newStatus = "Closed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClaimTicket_TwoAgentsClaimSimultaneously_OnlyOneSucceeds()
    {
        var (_, ticketId) = await CreateTicketAsCustomerAsync();
        var agentA = await CreateAgentAsync();
        var agentB = await CreateAgentAsync();

        var results = await Task.WhenAll(
            agentA.PostRawAsync($"/api/v1/tickets/{ticketId}/claim", new { }),
            agentB.PostRawAsync($"/api/v1/tickets/{ticketId}/claim", new { }));

        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successCount.Should().Be(1);
        conflictCount.Should().Be(1);

        // The failed attempt must not have left a partially-written history entry:
        // exactly the winner's 2 entries (status->InProgress, assignedAgentId) plus
        // the 1 creation entry should exist - never more.
        var winner = results.First(r => r.StatusCode == HttpStatusCode.OK);
        var winnerBody = await winner.Content.ReadFromJsonAsync<JsonElement>();
        winnerBody.GetProperty("status").GetString().Should().Be("InProgress");

        var historyClient = agentA;
        var history = await historyClient.GetAsync<JsonElement>($"/api/v1/tickets/{ticketId}/history");
        history.GetArrayLength().Should().Be(3);
    }
}
