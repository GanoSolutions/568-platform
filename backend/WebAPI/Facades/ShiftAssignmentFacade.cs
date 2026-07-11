using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class ShiftAssignmentFacade
	{
		private readonly Five68DbContext context_;

		public ShiftAssignmentFacade(Five68DbContext context)
		{
			context_ = context;
		}

		internal async Task<ShiftAssignment> CreateAsync(ShiftAssignment sa)
		{
			await context_.ShiftAssignments.AddAsync(sa);
			await context_.SaveChangesAsync();
			return sa;
		}

		internal async Task<ShiftAssignment> UpdateAsync(ShiftAssignment sa)
		{
			sa.UpdatedAt = DateTime.UtcNow;
			context_.ShiftAssignments.Update(sa);
			await context_.SaveChangesAsync();
			return sa;
		}

		internal async Task DeleteAsync(ShiftAssignment sa)
		{
			context_.ShiftAssignments.Remove(sa);
			await context_.SaveChangesAsync();
		}

		internal async Task<ShiftAssignment> FindByIdAsync(Guid id)
		{
			return await context_.ShiftAssignments
				.FirstOrDefaultAsync(x => x.Id == id);
		}

		internal async Task<ShiftAssignment> FindByDateAndEmployeeAsync(DateOnly date, Guid employeeId)
		{
			return await context_.ShiftAssignments
				.FirstOrDefaultAsync(x => x.Date == date && x.EmployeeId == employeeId);
		}

		internal async Task<IEnumerable<ShiftAssignment>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
		{
			return await context_.ShiftAssignments
				.Where(x => x.Date >= startDate && x.Date <= endDate)
				.OrderBy(x => x.Date)
				.ToListAsync();
		}

		internal async Task<bool> IsClosedDayAsync(DateOnly date)
		{
			return await context_.ClosedDays.AnyAsync(x => x.Date == date);
		}

	}
}