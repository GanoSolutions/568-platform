namespace Five68.Models.DTO;

/// <summary>An employee's profile data.</summary>
public class EmployeeDTO
{
	/// <summary>First name.</summary>
	public required string Name { get; set; }
	/// <summary>Last name.</summary>
	public required string Surname { get; set; }
	/// <summary>Italian fiscal code (codice fiscale).</summary>
	public required string FiscalCode { get; set; }
	/// <summary>Phone number.</summary>
	public required string Phone { get; set; }
	/// <summary>Display color used in the calendar UI, if set.</summary>
	public string? Color { get; set; }
	/// <summary>Contract end date, if the employee is on a fixed-term contract.</summary>
	public DateOnly? ContractEnd { get; set; }

	/// <summary>Maps an <see cref="Employee"/> entity to its DTO.</summary>
	public static EmployeeDTO? FromEmployee(Employee? emp)
	{
		return emp is null
			? null
			: new EmployeeDTO
			{
				Name = emp.Name,
				Surname = emp.Surname,
				FiscalCode = emp.FiscalCode,
				Phone = emp.Phone,
				ContractEnd = emp.ContractEnd,
				Color = emp.Color,
			};
	}
}