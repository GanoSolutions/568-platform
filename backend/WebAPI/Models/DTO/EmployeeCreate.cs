using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO;

/// <summary>Corpo della richiesta per creare o aggiornare un dipendente.</summary>
public class EmployeeCreate
{
	/// <summary>Nome del dipendente.</summary>
	[Required]
	public string Name { get; set; } = string.Empty;

	/// <summary>Cognome del dipendente.</summary>
	[Required]
	public string Surname { get; set; } = string.Empty;

	/// <summary>Codice fiscale, deve essere univoco.</summary>
	[Required]
	[MaxLength(16)]
	public string FiscalCode { get; set; } = string.Empty;

	/// <summary>Email, usata come username di login, deve essere univoca.</summary>
	[Required]
	[EmailAddress]
	public string Email { get; set; } = string.Empty;

	/// <summary>Numero di cellulare.</summary>
	[Required]
	[Phone]
	public string Phone { get; set; } = string.Empty;

	/// <summary>Data di fine contratto, opzionale.</summary>
	public DateOnly? ContractEnd { get; set; }
}