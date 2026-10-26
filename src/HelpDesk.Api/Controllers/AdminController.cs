using HelpDesk.Api.Contracts;
using HelpDesk.Application.Categories;
using HelpDesk.Application.Categories.CreateCategory;
using HelpDesk.Application.Categories.ListCategories;
using HelpDesk.Application.Categories.UpdateCategory;
using HelpDesk.Application.Reports.GetSummary;
using HelpDesk.Application.Users;
using HelpDesk.Application.Users.ChangeUserRole;
using HelpDesk.Application.Users.ListUsers;
using HelpDesk.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> ListUsers()
    {
        var result = await _sender.Send(new ListUsersQuery());
        return Ok(result);
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> ChangeUserRole(Guid id, ChangeUserRoleRequest request)
    {
        await _sender.Send(new ChangeUserRoleCommand(id, request.Role));
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyCollection<CategoryDto>>> ListCategories()
    {
        var result = await _sender.Send(new ListCategoriesQuery(IncludeInactive: true));
        return Ok(result);
    }

    [HttpPost("categories")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryRequest request)
    {
        var result = await _sender.Send(new CreateCategoryCommand(request.Name));
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("categories/{id:guid}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, UpdateCategoryRequest request)
    {
        var result = await _sender.Send(new UpdateCategoryCommand(id, request.Name, request.IsActive));
        return Ok(result);
    }

    [HttpGet("reports/summary")]
    public async Task<ActionResult<ReportSummaryDto>> ReportsSummary()
    {
        var result = await _sender.Send(new GetReportSummaryQuery());
        return Ok(result);
    }
}
