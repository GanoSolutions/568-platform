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
	}
}