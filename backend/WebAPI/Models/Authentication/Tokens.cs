using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class Tokens
{
	[Required]
	public required string AccessToken { get; set; }
	[Required]
	public required string RefreshToken { get; set; }
}