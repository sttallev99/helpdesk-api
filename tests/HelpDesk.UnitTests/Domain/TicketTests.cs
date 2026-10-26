using FluentAssertions;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Exceptions;
using Xunit;

namespace HelpDesk.UnitTests.Domain;

public class TicketTests
{
    private static Ticket CreateOpenTicket() =>
        Ticket.Create("TCK-000001", "Printer on fire", "Smoke from the office printer.", TicketPriority.High, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_SetsOpenStatusAndRecordsInitialHistoryEntry()
    {
        var ticket = CreateOpenTicket();

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.AssignedAgentId.Should().BeNull();
        ticket.History.Should().ContainSingle(h => h.Field == nameof(Ticket.Status) && h.OldValue == null && h.NewValue == TicketStatus.Open.ToString());
    }

    [Fact]
    public void Claim_WhenOpenAndUnassigned_AssignsAgentAndMovesToInProgress()
    {
        var ticket = CreateOpenTicket();
        var agentId = Guid.NewGuid();

        ticket.Claim(agentId);

        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssignedAgentId.Should().Be(agentId);
        ticket.History.Should().Contain(h => h.Field == nameof(Ticket.AssignedAgentId) && h.NewValue == agentId.ToString());
    }

    [Fact]
    public void Claim_WhenAlreadyAssigned_Throws()
    {
        var ticket = CreateOpenTicket();
        ticket.Claim(Guid.NewGuid());

        var act = () => ticket.Claim(Guid.NewGuid());

        act.Should().Throw<InvalidTicketOperationException>();
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingForCustomer, true)]
    [InlineData(TicketStatus.WaitingForCustomer, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.WaitingForCustomer, false)]
    [InlineData(TicketStatus.Open, TicketStatus.Open, false)]
    public void CanTransitionTo_MatchesAllowedTransitionTable(TicketStatus from, TicketStatus to, bool expected)
    {
        var ticket = CreateOpenTicket();
        MoveToStatus(ticket, from);

        ticket.CanTransitionTo(to).Should().Be(expected);
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ThrowsAndLeavesStatusUnchanged()
    {
        var ticket = CreateOpenTicket();
        MoveToStatus(ticket, TicketStatus.Closed);

        var act = () => ticket.ChangeStatus(TicketStatus.InProgress, Guid.NewGuid());

        act.Should().Throw<InvalidTicketTransitionException>();
        ticket.Status.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public void AddComment_CustomerReplyWhileWaitingForCustomer_ReopensTicket()
    {
        var ticket = CreateOpenTicket();
        var agentId = Guid.NewGuid();
        ticket.Claim(agentId);
        ticket.ChangeStatus(TicketStatus.WaitingForCustomer, agentId);

        ticket.AddComment(ticket.CustomerId, "Here is the info you asked for.", isInternal: false, isCustomerReply: true);

        ticket.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public void AddComment_AgentReplyWhileWaitingForCustomer_DoesNotChangeStatus()
    {
        var ticket = CreateOpenTicket();
        var agentId = Guid.NewGuid();
        ticket.Claim(agentId);
        ticket.ChangeStatus(TicketStatus.WaitingForCustomer, agentId);

        ticket.AddComment(agentId, "Just checking in.", isInternal: false, isCustomerReply: false);

        ticket.Status.Should().Be(TicketStatus.WaitingForCustomer);
    }

    [Fact]
    public void UpdateDetails_WhenResolved_Throws()
    {
        var ticket = CreateOpenTicket();
        var agentId = Guid.NewGuid();
        ticket.Claim(agentId);
        ticket.ChangeStatus(TicketStatus.Resolved, agentId);

        var act = () => ticket.UpdateDetails("New title", "New description", ticket.CategoryId, agentId);

        act.Should().Throw<InvalidTicketOperationException>();
    }

    [Fact]
    public void AddComment_InternalNote_IsMarkedInternal()
    {
        var ticket = CreateOpenTicket();
        var agentId = Guid.NewGuid();

        var comment = ticket.AddComment(agentId, "Escalating to facilities.", isInternal: true, isCustomerReply: false);

        comment.IsInternal.Should().BeTrue();
        ticket.Comments.Should().ContainSingle(c => c.Id == comment.Id);
    }

    private static void MoveToStatus(Ticket ticket, TicketStatus target)
    {
        if (target == TicketStatus.Open)
        {
            return;
        }

        var agentId = Guid.NewGuid();
        ticket.Claim(agentId);

        if (target == TicketStatus.InProgress)
        {
            return;
        }

        if (target == TicketStatus.WaitingForCustomer)
        {
            ticket.ChangeStatus(TicketStatus.WaitingForCustomer, agentId);
            return;
        }

        ticket.ChangeStatus(TicketStatus.Resolved, agentId);

        if (target == TicketStatus.Resolved)
        {
            return;
        }

        ticket.ChangeStatus(TicketStatus.Closed, agentId);
    }
}
