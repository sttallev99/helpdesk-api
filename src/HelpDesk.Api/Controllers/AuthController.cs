using System.Security.Claims;
using HelpDesk.Api.Contracts;
using HelpDesk.Application.Common.Interfaces;
using HelpDesk.Domain;
using HelpDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ValidationException = HelpDesk.Application.Common.Exceptions.ValidationException;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _tokenService;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    /// <summary>Self-service registration. New accounts are always created as Customers.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var ex = new ValidationException();
            ex.Errors["Identity"] = result.Errors.Select(e => e.Description).ToArray();
            throw ex;
        }

        await _userManager.AddToRoleAsync(user, Roles.Customer);

        var token = _tokenService.CreateToken(user.Id, user.Email!, user.FullName, [Roles.Customer]);

        return StatusCode(StatusCodes.Status201Created, new AuthResponse(token.Value, token.ExpiresAtUtc));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user.Id, user.Email!, user.FullName, roles);

        return Ok(new AuthResponse(token.Value, token.ExpiresAtUtc));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);

        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new MeResponse(user.Id, user.Email!, user.FullName, roles.ToList()));
    }
}
