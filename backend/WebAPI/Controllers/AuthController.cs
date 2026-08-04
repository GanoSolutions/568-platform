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

	/// <summary>Creates the controller with its required service.</summary>
	public AuthController(AuthService authService)
	{
		_authService = authService;
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
		string? email = User.FindFirst(ClaimTypes.Email)?.Value;

		if (email is null)
		{
			return Unauthorized();
		}
		await _authService.Logout(email);
		return Ok();
	}
}