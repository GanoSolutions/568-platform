using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

/// <summary>Request body to create a new user account.</summary>
public class UserRegister
{
	/// <summary>Login email.</summary>
	[Required]
	[EmailAddress]
	public required string Email { get; set; }
	/// <summary>Initial password.</summary>
	[Required]
	public required string Password { get; set; }
	/// <summary>Permission level to assign.</summary>
	public UserRole Role { get; set; }
}