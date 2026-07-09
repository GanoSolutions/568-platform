using Microsoft.AspNetCore.Authorization;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Five68.Services;
using Five68.Models.DTO;
using System.Security.Claims;

namespace Five68.Controllers
{
	[Route("[controller]")]
	[ApiController]
	[Authorize]
	public class ShiftAssignmentController : Controller
	{
		private readonly ShiftAssignmentService _shiftAssignmentService;

		public ShiftAssignmentController(ShiftAssignmentService shiftAssignmentService)
		{
			_shiftAssignmentService = shiftAssignmentService;
		}

		/// <summary>
		/// Returns a single shift assignment by ID.
		/// </summary>
		/// <param name="id">The ID of the shift assignment to retrieve.</param>
		/// <response code="200">Shift assignment found.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="404">Shift assignment not found.</response>
		[HttpGet("{id:guid}")]
		public async Task<IActionResult> GetById(Guid id)
		{
			ShiftAssignmentDTO dto = await _shiftAssignmentService.GetById(id);
			if (dto is null)
			{
				return NotFound();
			}

			return Ok(dto);
		}

		/// <summary>
		/// Returns all shift assignments whose date falls within the given range (inclusive), ordered by date.
		/// </summary>
		/// <param name="startDate">Start of the date range.</param>
		/// <param name="endDate">End of the date range.</param>
		/// <response code="200">List of shift assignments.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="422"><paramref name="endDate"/> is before <paramref name="startDate"/>.</response>
		[HttpGet("")]
		public async Task<IActionResult> GetByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
		{
			IEnumerable<ShiftAssignmentDTO> list = await _shiftAssignmentService.GetByDateRange(startDate, endDate);
			return Ok(list);
		}

		/// <summary>
		/// Creates a new shift assignment for an employee. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="model">The employee, date and time range to assign.</param>
		/// <response code="201">Shift assignment created successfully.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Employee not found.</response>
		/// <response code="422">End time is not after start time, the day is closed, or the employee is already assigned on that date.</response>
		[HttpPost("")]
		public async Task<IActionResult> Create([FromBody] ShiftAssignmentCreate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}
			return Created(string.Empty, await _shiftAssignmentService.Create(model, requesterId));
		}

		/// <summary>
		/// Updates the start/end time of an existing shift assignment. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="id">The ID of the shift assignment to update.</param>
		/// <param name="model">The new time range.</param>
		/// <response code="200">Shift assignment updated successfully.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Shift assignment not found.</response>
		/// <response code="422">End time is not after start time, or the day is closed.</response>
		[HttpPut("{id:guid}")]
		public async Task<IActionResult> Update(Guid id, [FromBody] ShiftAssignmentUpdate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			ShiftAssignmentDTO dto = await _shiftAssignmentService.Update(id, model, requesterId);
			return Ok(dto);
		}

		/// <summary>
		/// Deletes a shift assignment. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="id">The ID of the shift assignment to delete.</param>
		/// <response code="204">Shift assignment deleted successfully.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Shift assignment not found.</response>
		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> Delete(Guid id)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			await _shiftAssignmentService.Delete(id, requesterId);
			return NoContent();
		}

		private Guid GetRequesterId()
		{
			string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
		}

	}
}