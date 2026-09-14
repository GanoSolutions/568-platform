using Five68.Models;
using Five68.Models.Authentication;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers
{
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
			string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (userId is null || !Guid.TryParse(userId, out Guid id))
				return Unauthorized();

			string? email = User.FindFirst(ClaimTypes.Email)?.Value;
			logger_?.LogInformation($"User {id} ({email}) required logout");

			await authService_.Logout(id);
			return Ok();
		}
	}
}