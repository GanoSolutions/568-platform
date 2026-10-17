using Five68.Utils;
using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO;

/// <summary>Request body to register a browser Web Push subscription.</summary>
public class PushSubscriptionCreate
{
	/// <summary>The push service endpoint URL returned by the browser.</summary>
	[Required]
	[MaxLength(2048)]
	[PushServiceEndpoint]
	public string Endpoint { get; set; } = string.Empty;
	/// <summary>The client's P-256 ECDH public key (base64url).</summary>
	[Required]
	[MaxLength(256)]
	public string P256dh { get; set; } = string.Empty;
	/// <summary>The client's authentication secret (base64url).</summary>
	[Required]
	[MaxLength(256)]
	public string Auth { get; set; } = string.Empty;
	/// <summary>The browser's user agent, if provided.</summary>
	[MaxLength(512)]
	public string? UserAgent { get; set; }
}

/// <summary>Request body to remove a Web Push subscription.</summary>
public class PushUnsubscribe
{
	/// <summary>The endpoint of the subscription to remove.</summary>
	[Required]
	[MaxLength(2048)]
	public string Endpoint { get; set; } = string.Empty;
}