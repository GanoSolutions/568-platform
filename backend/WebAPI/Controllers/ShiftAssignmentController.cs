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

		[HttpGet("(id:guid)")]
		public async Task<IActionResult> GetById(Guid id)
		{
			ShiftAssignmentDTO dto = await _shiftAssignmentService.GetById(id);
			if (dto is null)
			{
				return NotFound();
			}

			return Ok(dto);
		}

		[HttpGet("")]
		public async Task<IActionResult> GetByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
		{
			IEnumerable<ShiftAssignmentDTO> list = await _shiftAssignmentService.GetByDateRange(startDate, endDate);
			return Ok(list);
		}

		[HttpPost("")]
		public async Task<IActionResult> Create([FromBody] ShiftAssignmentCreate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}
			await _shiftAssignmentService.Create(model, requesterId);
			return Created();
		}

		private Guid GetRequesterId()
		{
			string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
		}

	}
}