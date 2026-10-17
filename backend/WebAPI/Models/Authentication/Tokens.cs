using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

/// <summary>An access/refresh token pair, also used as the refresh request body.</summary>
public class Tokens
{
	/// <summary>JWT used to authenticate requests.</summary>
	[Required]
	public required string AccessToken { get; set; }
	/// <summary>Token used to obtain a new access token once it expires.</summary>
	[Required]
	public required string RefreshToken { get; set; }
}