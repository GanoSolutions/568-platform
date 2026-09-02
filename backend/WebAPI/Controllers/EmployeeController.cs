using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Five68.Services;
using Five68.Models.DTO;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers
{
	[Route("[controller]")]
	[ApiController]
	[Authorize]
	public class EmployeeController : Controller
	{
		private readonly EmployeeService _employeeService;

		public EmployeeController(EmployeeService employeeService)
		{
			_employeeService = employeeService;
		}

		/// <summary>
		/// Restituisce l'elenco dei dipendenti attivi — solo dati anagrafici (niente email/stato
		/// account, disponibili separatamente via <c>GET /user</c>).
		/// </summary>
		/// <response code="200">Elenco dipendenti.</response>
		/// <response code="401">Il chiamante non è autenticato.</response>
		[HttpGet("")]
		[HttpGet("/employees")]
		public async Task<IActionResult> GetAll()
		{
			return Ok(await _employeeService.GetAll());
		}

		/// <summary>Crea un nuovo dipendente (User + Employee). Solo manager/admin.</summary>
		/// <param name="model">Anagrafica del dipendente.</param>
		/// <response code="201">Dipendente creato.</response>
		/// <response code="401">Il chiamante non è autenticato.</response>
		/// <response code="403">Il chiamante non è manager o admin.</response>
		/// <response code="422">Email o codice fiscale già in uso.</response>
		[HttpPost("")]
		public async Task<IActionResult> Create([FromBody] EmployeeCreate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			return Created(string.Empty, await _employeeService.Create(model, requesterId));
		}

		/// <summary>Aggiorna l'anagrafica di un dipendente. Solo manager/admin.</summary>
		/// <param name="id">L'ID del dipendente da aggiornare.</param>
		/// <param name="model">I nuovi dati anagrafici.</param>
		/// <response code="200">Dipendente aggiornato.</response>
		/// <response code="401">Il chiamante non è autenticato.</response>
		/// <response code="403">Il chiamante non è manager o admin.</response>
		/// <response code="404">Dipendente non trovato.</response>
		/// <response code="422">Email o codice fiscale già in uso.</response>
		[HttpPut("{id:guid}")]
		public async Task<IActionResult> Update(Guid id, [FromBody] EmployeeCreate model)
		{
			Guid requesterId = GetRequesterId();
			if (requesterId == Guid.Empty)
			{
				return Unauthorized();
			}

			return Ok(await _employeeService.Update(id, model, requesterId));
		}

		private Guid GetRequesterId()
		{
			string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
		}
	}
}