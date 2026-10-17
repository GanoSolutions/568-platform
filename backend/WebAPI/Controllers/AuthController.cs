using Five68.Models.Authentication;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers;

/// <summary>Authentication: login, token refresh, logout.</summary>
[Route("[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
	private readonly AuthService _authService;
	private readonly ILogger? _logger;

	/// <summary>Creates the controller with its required service and an optional logger.</summary>
	public AuthController(AuthService authService, ILogger<AuthController>? logger = null)
	{
		_authService = authService;
		_logger = logger;
	}

	/// <summary>Logs in with email and password, returning an access/refresh token pair.</summary>
	[HttpPost("login")]
	public async Task<IActionResult> Login(UserLogin userData)
	{
		Tokens token = await _authService.Login(userData);
		return Ok(token);
	}

	/// <summary>Exchanges a valid refresh token for a new access/refresh token pair.</summary>
	[HttpPost("refresh")]
	public async Task<IActionResult> Refresh(Tokens token)
	{
		Tokens newJwtToken = await _authService.Refresh(token);
		return Ok(newJwtToken);
	}

	/// <summary>
	/// Logout the user from the platform, the token will still be valid for the next 5 minutes at most
	/// </summary>
	[Authorize]
	[HttpPost("logout")]
	public async Task<IActionResult> Logout()
	{
		string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (userId is null || !Guid.TryParse(userId, out Guid id))
		{
			return Unauthorized();
		}

		string? email = User.FindFirst(ClaimTypes.Email)?.Value;
		_logger?.LogInformation("User {UserId} ({UserEmail}) required logout", id, email);

		await _authService.Logout(id);
		return Ok();
	}
}