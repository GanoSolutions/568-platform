using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO
{
	public class EmployeeCreate
	{
		/// <summary>Nome del dipendente.</summary>
		[Required]
		public string Name { get; set; }

		/// <summary>Cognome del dipendente.</summary>
		[Required]
		public string Surname { get; set; }

		/// <summary>Codice fiscale, deve essere univoco.</summary>
		[Required]
		public string FiscalCode { get; set; }

		/// <summary>Email, usata come username di login, deve essere univoca.</summary>
		[Required]
		[EmailAddress]
		public string Email { get; set; }

		/// <summary>Numero di cellulare.</summary>
		[Required]
		public string Phone { get; set; }

		/// <summary>Data di fine contratto, opzionale.</summary>
		public DateOnly? ContractEnd { get; set; }
	}
}
