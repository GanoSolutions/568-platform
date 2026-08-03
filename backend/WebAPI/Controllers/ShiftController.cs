using Five68.Models.DTO;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers
{
	[Route("[controller]")]
	[ApiController]
	[Authorize]
	public class ShiftController : Controller
	{
		private readonly ShiftService _shiftService;

		public ShiftController(ShiftService shiftService)
		{
			_shiftService = shiftService;
		}

		/// <summary>
		/// Returns a single shift by ID.
		/// </summary>
		/// <param name="id">The ID of the shift to retrieve.</param>
		/// <response code="200">Shift found.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="404">Shift not found.</response>
		[HttpGet("{id:guid}")]
		public async Task<IActionResult> GetById(Guid id)
		{
			ShiftDTO dto = await _shiftService.GetById(id);
			if (dto is null)
			{
				return NotFound();
			}

			return Ok(dto);
		}

		/// <summary>
		/// Returns all shifts whose date falls within the given range (inclusive), ordered by date.
		/// </summary>
		/// <param name="startDate">Start of the date range.</param>
		/// <param name="endDate">End of the date range.</param>
		/// <response code="200">List of shifts.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="422"><paramref name="endDate"/> is before <paramref name="startDate"/>.</response>
		[HttpGet("")]
		public async Task<IActionResult> GetByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
		{
			IEnumerable<ShiftDTO> list = await _shiftService.GetByDateRange(startDate, endDate);
			return Ok(list);
		}

		/// <summary>
		/// Creates a new shift for an employee. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="model">The employee, date, start time and duration to assign.</param>
		/// <response code="201">Shift created successfully.</response>
		/// <response code="400">Duration is zero or exceeds 24 hours.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Employee not found.</response>
		/// <response code="422">The day is closed, or the employee is already assigned on that date.</response>
		[HttpPost("")]
		public async Task<IActionResult> Create([FromBody] ShiftCreate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}
			return Created(string.Empty, await _shiftService.Create(model, requesterId));
		}

		/// <summary>
		/// Updates the start time/duration of an existing shift. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="id">The ID of the shift to update.</param>
		/// <param name="model">The new start time and duration.</param>
		/// <response code="200">Shift updated successfully.</response>
		/// <response code="400">Duration is zero or exceeds 24 hours.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Shift not found.</response>
		/// <response code="422">The day is closed.</response>
		[HttpPut("{id:guid}")]
		public async Task<IActionResult> Update(Guid id, [FromBody] ShiftUpdate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			ShiftDTO dto = await _shiftService.Update(id, model, requesterId);
			return Ok(dto);
		}

		/// <summary>
		/// Deletes a shift. Only managers/admins can perform this action.
		/// </summary>
		/// <param name="id">The ID of the shift to delete.</param>
		/// <response code="204">Shift deleted successfully.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="403">Caller is not a manager or admin.</response>
		/// <response code="404">Shift not found.</response>
		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> Delete(Guid id)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			await _shiftService.Delete(id, requesterId);
			return NoContent();
		}

		private Guid GetRequesterId()
		{
			string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
		}
	}
}