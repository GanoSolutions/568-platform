using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class Tokens
{
	[Required]
	public string AccessToken { get; set; }
	[Required]
	public string RefreshToken { get; set; }
}