using System.ComponentModel.DataAnnotations;

namespace Five68.Models.Authentication
{
	public class InviteAccept
	{
		[Required]
		public string Token { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		public string Surname { get; set; }
		[Required]
		[RegularExpression(@"^[A-Za-z]{6}[0-9]{2}[A-Za-z][0-9]{2}[A-Za-z][0-9]{3}[A-Za-z]$", ErrorMessage = "Codice fiscale non valido")]
		public string FiscalCode { get; set; }
		[Required]
		[Phone]
		public string Phone { get; set; }
		[Required]
		public string Password { get; set; }
	}
}