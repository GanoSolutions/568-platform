using Five68.Models.DTO;
using Five68.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Five68.Controllers;

/// <summary>Web Push: VAPID key, subscribe/unsubscribe, subscription status.</summary>
[Route("[controller]")]
[ApiController]
[Authorize]
public class PushController : Controller
{
	private readonly PushSubscriptionService _service;
	private readonly WebPushSettings _webPushSettings;

	/// <summary>Creates the controller with its required service and the Web Push settings.</summary>
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

	/// <summary>Disables one of the caller's push subscriptions. Idempotent.</summary>
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

	/// <summary>Tells whether the caller has at least one active push subscription.</summary>
	[HttpGet("status")]
	public async Task<IActionResult> Status()
	{
		Guid userId = GetRequesterId();
		return userId == Guid.Empty ? Unauthorized() : Ok(new { active = await _service.HasActiveSubscription(userId) });
	}

	private Guid GetRequesterId()
	{
		string? requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		return Guid.TryParse(requesterId, out Guid id) ? id : Guid.Empty;
	}
}