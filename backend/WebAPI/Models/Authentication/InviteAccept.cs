using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

/// <summary>Request body to accept an invite and activate the account.</summary>
public class InviteAccept
{
	/// <summary>The invite token received out-of-band.</summary>
	[Required]
	public required string Token { get; set; }
	/// <summary>The password to set for the account.</summary>
	[Required]
	public required string Password { get; set; }
}