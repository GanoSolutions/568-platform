using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class UserLogin
{
	[Required]
	[EmailAddress]
	public required string Email { get; set; }
	[Required]
	public required string Password { get; set; }
}