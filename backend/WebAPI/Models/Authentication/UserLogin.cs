using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class UserLogin
{
	[Required]
	[EmailAddress]
	public string Email { get; set; }
	[Required]
	public string Password { get; set; }
}