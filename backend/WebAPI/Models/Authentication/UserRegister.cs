using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class UserRegister
{
	[Required]
	[EmailAddress]
	public required string Email { get; set; }
	[Required]
	public required string Password { get; set; }
	public UserRole Role { get; set; }
}