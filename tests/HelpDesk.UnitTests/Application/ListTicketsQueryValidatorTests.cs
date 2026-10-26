using FluentAssertions;
using HelpDesk.Application.Tickets.ListTickets;
using Xunit;

namespace HelpDesk.UnitTests.Application;

public class ListTicketsQueryValidatorTests
{
    private readonly ListTicketsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultPaging_Succeeds()
    {
        var result = _validator.Validate(new ListTicketsQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithPageNumberBelowOne_Fails()
    {
        var result = _validator.Validate(new ListTicketsQuery(PageNumber: 0));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WithPageSizeOutsideAllowedRange_Fails(int pageSize)
    {
        var result = _validator.Validate(new ListTicketsQuery(PageSize: pageSize));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMaxAllowedPageSize_Succeeds()
    {
        var result = _validator.Validate(new ListTicketsQuery(PageSize: ListTicketsQueryValidator.MaxPageSize));

        result.IsValid.Should().BeTrue();
    }
}
