using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers;

[Route("[controller]")]
[ApiController]
[Authorize]
public class SettingsController : Controller
{
	private readonly SettingsService _settingsService;

	public SettingsController(SettingsService settingsService)
	{
		_settingsService = settingsService;
	}

	/// <summary>
	/// Returns all closed days whose date falls within the given range (inclusive), ordered by date.
	/// </summary>
	/// <param name="startDate">Start of the date range.</param>
	/// <param name="endDate">End of the date range.</param>
	/// <response code="200">List of closed days.</response>
	/// <response code="401">Caller is not authenticated.</response>
	/// <response code="422"><paramref name="endDate"/> is before <paramref name="startDate"/>.</response>
	[HttpGet("closed-days")]
	public async Task<IActionResult> GetClosedDaysByDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
	{
		return Ok(await _settingsService.GetClosedDaysByDateRange(startDate, endDate));
	}
}