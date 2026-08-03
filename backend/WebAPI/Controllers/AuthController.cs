using Five68.Models.Authentication;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
	private readonly AuthService authService_;
	private readonly ILogger logger_;

	public AuthController(AuthService authService, ILogger<AuthController> logger = null)
	{
		authService_ = authService;
		logger_ = logger;
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login(UserLogin userData)
	{
		Tokens token = await authService_.Login(userData);

		logger_.LogInformation("User " + userData.Email + " has logged in");

		return Ok(token);
	}

	[HttpPost("refresh")]
	public async Task<IActionResult> Refresh(Tokens token)
	{
		Tokens newJwtToken = await authService_.Refresh(token);
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
			return Unauthorized();

		logger_?.LogInformation($"User {email} required logout");

		await authService_.Logout(email);
		return Ok();
	}
}