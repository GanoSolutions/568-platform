using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades;

public class EmployeeFacade
{
	private readonly Five68DbContext _context;

	public EmployeeFacade(Five68DbContext context)
	{
		_context = context;
	}

	internal async Task CreateAsync(Employee emp)
	{
		await _context.Employees.AddAsync(emp);
		await _context.SaveChangesAsync();
	}

	internal async Task UpdateAsync(Employee emp)
	{
		emp.UpdatedAt = DateTime.UtcNow;

		_context.Employees.Update(emp);
		await _context.SaveChangesAsync();
	}

	internal async Task<Employee?> FindByIdAsync(Guid userId)
	{
		return await _context.Employees.FirstOrDefaultAsync(x => x.UserId == userId);
	}
}