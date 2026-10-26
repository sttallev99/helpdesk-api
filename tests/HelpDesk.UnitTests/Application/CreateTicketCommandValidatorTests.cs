using FluentAssertions;
using HelpDesk.Application.Tickets.CreateTicket;
using HelpDesk.Domain.Enums;
using Xunit;

namespace HelpDesk.UnitTests.Application;

public class CreateTicketCommandValidatorTests
{
    private readonly CreateTicketCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Succeeds()
    {
        var command = new CreateTicketCommand("Title", "Description", TicketPriority.Normal, Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Description")]
    [InlineData("Title", "")]
    public void Validate_WithEmptyRequiredField_Fails(string title, string description)
    {
        var command = new CreateTicketCommand(title, description, TicketPriority.Normal, Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyCategoryId_Fails()
    {
        var command = new CreateTicketCommand("Title", "Description", TicketPriority.Normal, Guid.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTitleExceedingMaxLength_Fails()
    {
        var command = new CreateTicketCommand(new string('a', 201), "Description", TicketPriority.Normal, Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
