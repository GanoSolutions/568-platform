using Five68.Models.Authentication;
using Five68.Models.DTO;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers;

[Route("[controller]")]
[ApiController]
[Authorize]
public class UserController : Controller
{
	private readonly UserService userService_;
	private readonly ILogger logger_;

	public UserController(UserService userService, ILogger<UserController> logger = null)
	{
		userService_ = userService;
		logger_ = logger;
	}

	/// <summary>
	/// Returns a single user by ID.
	/// </summary>
	/// <param name="id">The ID of the user to retrieve.</param>
	/// <response code="200">User found.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="404">User not found.</response>
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetUserById(Guid id)
	{

		UserDTO user = await userService_.GetUserDTO(id);

		if (user is null)
		{
			return NotFound();
		}

		return Ok(user);
	}

	/// <summary>
	/// Returns all users.
	/// </summary>
	/// <response code="200">List of users.</response>
	/// <response code="401">Caller is not authenticated.</response>
	[HttpGet("")]
	public async Task<IActionResult> GetUsers()
	{
		IEnumerable<UserDTO> users = await userService_.GetAll();
		return Ok(users);
	}

	/// <summary>
	/// Creates a new user account. The caller can only assign roles strictly below their own.
	/// The created user is set to Pending status until they accept the invite.
	/// </summary>
	/// <response code="201">User created successfully.</response>
	/// <response code="400">Validation failed (missing fields or weak password).</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="403">Caller attempted to create a user with equal or higher role.</response>
	/// <response code="422">Email is already in use.</response>
	[Authorize]
	[HttpPost("signup")]
	public async Task<IActionResult> Signup([FromBody] UserRegister model)
	{
		string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!Guid.TryParse(requesterId, out Guid id))
		{
			return Unauthorized();
		}
		await userService_.CreateUser(model, id);
		return Created();
	}

	/// <summary>
	/// Generates an invite token for a user and returns it. The token expires after 7 days.
	/// Can be called again to regenerate an expired token.
	/// </summary>
	/// <param name="id">The ID of the user to invite.</param>
	/// <response code="200">Invite token generated. Response contains <c>inviteToken</c>.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="404">User not found.</response>
	[Authorize]
	[HttpPost("{id:guid}/invite")]
	public async Task<IActionResult> Invite(Guid id)
	{
		string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!Guid.TryParse(requesterId, out Guid reqGuid))
		{
			return Unauthorized();
		}

		string token = await userService_.GenerateInvite(id, reqGuid);
		return Ok(new { inviteToken = token });
	}

	/// <summary>
	/// Accepts an invite and sets the user's password. No authentication required —
	/// the invite token acts as the credential. Sets the account to Active on success.
	/// </summary>
	/// <response code="200">Password set, account activated.</response>
	/// <response code="400">Validation failed (missing token or password).</response>
	/// <response code="401">Token is invalid or expired.</response>
	[AllowAnonymous]
	[HttpPost("invite/accept")]
	public async Task<IActionResult> AcceptInvite([FromBody] InviteAccept model)
	{
		await userService_.AcceptInvite(model);
		return Ok();
	}

	/// <summary>
	/// Changes the authenticated user's password. Requires the current password as verification.
	/// The new password is validated against password requirements and hashed before saving.
	/// </summary>
	/// <response code="204">Password changed successfully.</response>
	/// <response code="400">New password does not meet requirements.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="422">Current password is incorrect.</response>
	[Authorize]
	[HttpPost("password")]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePassword model)
	{
		string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		if (!Guid.TryParse(requesterId, out Guid id))
			return Unauthorized();
		await userService_.ChangePassword(id, model);
		return NoContent();
	}
}