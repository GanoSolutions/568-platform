namespace Five68.Models.DTO;

public class EmployeeDTO
{
	public required string Name { get; set; }
	public required string Surname { get; set; }
	public required string FiscalCode { get; set; }
	public required string Phone { get; set; }
	public string? Color { get; set; }
	public DateOnly? ContractEnd { get; set; }

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