using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

/// <summary>Request body to log in.</summary>
public class UserLogin
{
	/// <summary>Login email.</summary>
	[Required]
	[EmailAddress]
	public required string Email { get; set; }
	/// <summary>Account password.</summary>
	[Required]
	public required string Password { get; set; }
}