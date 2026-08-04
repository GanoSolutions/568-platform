using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

public class InviteAccept
{
	[Required]
	public required string Token { get; set; }
	[Required]
	public required string Name { get; set; }
	[Required]
	public required string Surname { get; set; }
	[Required]
	[RegularExpression(@"^[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]$", ErrorMessage = "Codice fiscale non valido")]
	public required string FiscalCode { get; set; }
	[Required]
	[Phone]
	public required string Phone { get; set; }
	[Required]
	public required string Password { get; set; }
}