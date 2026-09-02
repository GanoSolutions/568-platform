using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class EmployeeFacade
	{
		private readonly Five68DbContext context_;

		public EmployeeFacade(Five68DbContext context)
		{
			context_ = context;
		}

		internal async Task CreateAsync(Employee emp)
		{
			await context_.Employees.AddAsync(emp);
			await context_.SaveChangesAsync();
		}

		internal async Task UpdateAsync(Employee emp)
		{
			emp.UpdatedAt = DateTime.UtcNow;

			context_.Employees.Update(emp);
			await context_.SaveChangesAsync();
		}

		internal async Task<Employee> FindByIdAsync(Guid userId)
		{
			return await context_.Employees.FirstOrDefaultAsync(x => x.UserId == userId);
		}

		internal async Task<IEnumerable<Employee>> GetAllAsync()
		{
			return await context_.Employees
				.Where(x => context_.Users.Any(u => u.Id == x.UserId && u.Status != UserStatus.Disabled))
				.ToListAsync();
		}

		internal async Task<Employee> FindByFiscalCodeAsync(string fiscalCode)
		{
			return await context_.Employees.FirstOrDefaultAsync(x => x.FiscalCode == fiscalCode);
		}


	}
}