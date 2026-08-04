using Five68.Models;
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
public class SwapRequestController : Controller
{
	private readonly SwapRequestService _swapRequestService;
	private readonly UserService _userService;

	public SwapRequestController(SwapRequestService swapRequestService, UserService userService)
	{
		_swapRequestService = swapRequestService;
		_userService = userService;
	}

	/// <summary>
	/// Returns the swap requests visible to the caller: all of them for Admin/Manager,
	/// only the ones where the caller is requester or target for Employee.
	/// </summary>
	/// <response code="200">List of swap requests.</response>
	/// <response code="401">Caller is not authenticated.</response>
	[HttpGet("")]
	public async Task<IActionResult> GetForUser()
	{
		Guid requesterId = GetRequesterId();
		if (requesterId == Guid.Empty)
		{
			return Unauthorized();
		}

		User? user = await _userService.Get(requesterId);
		return user is null ? Unauthorized() : Ok(await _swapRequestService.GetForUser(requesterId, user.Role));
	}

	/// <summary>
	/// Creates one swap request per selected colleague for a shift owned by the caller.
	/// </summary>
	/// <response code="201">Swap request(s) created.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="403">Caller does not own the shift.</response>
	/// <response code="404">Shift or target employee not found.</response>
	/// <response code="422">Target already has a shift that day, or a duplicate pending request exists.</response>
	[HttpPost("")]
	public async Task<IActionResult> Create([FromBody] SwapRequestCreate model)
	{
		Guid requesterId = GetRequesterId();
		return requesterId == Guid.Empty ? Unauthorized() : Created(string.Empty, await _swapRequestService.Create(model, requesterId));
	}

	/// <summary>
	/// Accepts a pending swap request. Only the target employee or an Admin can respond.
	/// </summary>
	/// <response code="200">Swap request accepted.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="403">Caller is neither the target employee nor an Admin.</response>
	/// <response code="404">Swap request not found.</response>
	/// <response code="422">The request was already handled, or the target now has a conflicting shift.</response>
	[HttpPost("{id:guid}/accept")]
	public async Task<IActionResult> Accept(Guid id)
	{
		Guid requesterId = GetRequesterId();
		return requesterId == Guid.Empty ? Unauthorized() : Ok(await _swapRequestService.Accept(id, requesterId));
	}

	/// <summary>
	/// Rejects a pending swap request. Only the target employee or an Admin can respond.
	/// </summary>
	/// <response code="200">Swap request rejected.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="403">Caller is neither the target employee nor an Admin.</response>
	/// <response code="404">Swap request not found.</response>
	/// <response code="422">The request was already handled.</response>
	[HttpPost("{id:guid}/reject")]
	public async Task<IActionResult> Reject(Guid id)
	{
		Guid requesterId = GetRequesterId();
		return requesterId == Guid.Empty ? Unauthorized() : Ok(await _swapRequestService.Reject(id, requesterId));
	}

	private Guid GetRequesterId()
	{
		string? requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
	}

	/// <summary>
	/// Cancels a pending swap request. Only the original requester or an Admin can cancel it.
	/// </summary>
	/// <response code="200">Swap request cancelled.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="403">Caller is neither the requester nor an Admin.</response>
	/// <response code="404">Swap request not found.</response>
	/// <response code="422">The request was already handled.</response>
	[HttpPost("{id:guid}/cancel")]
	public async Task<IActionResult> Cancel(Guid id)
	{
		Guid requesterId = GetRequesterId();
		return requesterId == Guid.Empty ? Unauthorized() : Ok(await _swapRequestService.Cancel(id, requesterId));
	}
}