namespace Five68.Models.DTO;

public class EmployeeDTO
{
	public string Name { get; set; }
	public string Surname { get; set; }
	public string FiscalCode { get; set; }
	public string Phone { get; set; }
	public string? Color { get; set; }
	public DateOnly? ContractEnd { get; set; }

	public static EmployeeDTO FromEmployee(Employee emp)
	{
		if (emp is null)
		{
			return null;
		}

		return new EmployeeDTO
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