using HelpDesk.Application.Categories;
using HelpDesk.Application.Categories.ListCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>Read-only, active-only category list for building the "create ticket" form.</summary>
[ApiController]
[Authorize]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public CategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoryDto>>> List()
    {
        var result = await _sender.Send(new ListCategoriesQuery(IncludeInactive: false));
        return Ok(result);
    }
}
