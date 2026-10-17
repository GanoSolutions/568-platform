using Five68.Models.DTO;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers
{
	[Route("[controller]")]
	[ApiController]
	[Authorize]
	public class PushController : Controller
	{
		private readonly PushSubscriptionService _service;
		private readonly WebPushSettings _webPushSettings;
		public PushController(PushSubscriptionService service, IOptions<AppSettings> settings)
		{
			_service = service;
			_webPushSettings = settings.Value.WebPush;
		}

		/// <summary>
		/// Returns the VAPID public key the browser needs.
		/// </summary>
		/// <response code="200">Web Push is enabled: body is { publicKey }.</response>
		/// <response code="401">Caller is not authenticated.</response>
		/// <response code="404">Web Push is disabled or not configured on this server.</response>
		[HttpGet("vapid-public-key")]
		public IActionResult VapidPublicKey()
		{
			if (!_webPushSettings.Enabled || string.IsNullOrWhiteSpace(_webPushSettings.PublicKey))
			{
				return NotFound();
			}

			// Solo la chiave pubblica: mai serializzare WebPushSettings, che contiene anche la PrivateKey
			return Ok(new { publicKey = _webPushSettings.PublicKey });
		}
		/// <summary>
		/// Registers (or re-assigns) a Web Push subscription for the authenticated user.
		/// </summary>
		/// <response code="204">Subscription registered.</response>
		/// <response code="400">Malformed subscription payload (model validation).</response>
		/// <response code="401">Caller is not authenticated.</response>
		[HttpPost("subscribe")]
		public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionCreate model)
		{
			Guid userId = GetRequesterId();
			if (userId == Guid.Empty)
			{
				return Unauthorized();
			}

			await _service.Subscribe(model, userId);
			return NoContent();
		}

		/// <summary>Deletes one of the caller's push subscriptions. Idempotent</summary>
		[HttpPost("unsubscribe")]
		public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribe model)
		{
			Guid userId = GetRequesterId();
			if (userId == Guid.Empty)
			{
				return Unauthorized();
			}

			await _service.Unsubscribe(model.Endpoint, userId);
			return NoContent();
		}

		/// <summary>
		/// Returns the caller's push subscription endpoint, if any. The client compares it with the
		/// browser's own subscription to tell "active on this device" from "active on another device".
		/// </summary>
		/// <response code="200">{ active, endpoint } — endpoint is null when there is no subscription.</response>
		[HttpGet("status")]
		public async Task<IActionResult> Status()
		{
			Guid userId = GetRequesterId();
			if (userId == Guid.Empty)
			{
				return Unauthorized();
			}

			string endpoint = await _service.GetSubscribedEndpoint(userId);
			return Ok(new { active = endpoint is not null, endpoint });
		}

		private Guid GetRequesterId()
		{
			string requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
		}
	}
}