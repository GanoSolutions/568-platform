using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication;

/// <summary>Request body to accept an invite and activate the account.</summary>
public class InviteAccept
{
	/// <summary>The invite token received out-of-band.</summary>
	[Required]
	public required string Token { get; set; }
	/// <summary>First name.</summary>
	[Required]
	public required string Name { get; set; }
	/// <summary>Last name.</summary>
	[Required]
	public required string Surname { get; set; }
	/// <summary>Italian fiscal code (codice fiscale).</summary>
	[Required]
	[RegularExpression(@"^[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]$", ErrorMessage = "Codice fiscale non valido")]
	public required string FiscalCode { get; set; }
	/// <summary>Phone number.</summary>
	[Required]
	[Phone]
	public required string Phone { get; set; }
	/// <summary>The password to set for the account.</summary>
	[Required]
	public required string Password { get; set; }
}