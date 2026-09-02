using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class ShiftFacade
	{
		private readonly Five68DbContext context_;

		public ShiftFacade(Five68DbContext context)
		{
			context_ = context;
		}

		internal async Task<Shift> CreateAsync(Shift shift)
		{
			await context_.Shifts.AddAsync(shift);
			await context_.SaveChangesAsync();
			return shift;
		}

		internal async Task<Shift> UpdateAsync(Shift shift)
		{
			shift.UpdatedAt = DateTime.UtcNow;
			context_.Shifts.Update(shift);
			await context_.SaveChangesAsync();
			return shift;
		}

		internal async Task DeleteAsync(Shift shift)
		{
			context_.Shifts.Remove(shift);
			await context_.SaveChangesAsync();
		}

		internal async Task<Shift> FindByIdAsync(Guid id)
		{
			return await context_.Shifts
				.FirstOrDefaultAsync(x => x.Id == id);
		}

		internal async Task<Shift> FindByDateAndEmployeeAsync(DateOnly date, Guid employeeId)
		{
			return await context_.Shifts
				.FirstOrDefaultAsync(x => x.Date == date && x.EmployeeId == employeeId);
		}

		internal async Task<IEnumerable<Shift>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
		{
			return await context_.Shifts
				.Where(x => x.Date >= startDate && x.Date <= endDate)
				.OrderBy(x => x.Date)
				.ToListAsync();
		}

		internal async Task<bool> IsClosedDayAsync(DateOnly date)
		{
			return await context_.ClosedDays.AnyAsync(x => x.Date == date);
		}

		internal async Task DeleteFutureAssignmentsForEmployeeAsync(Guid employeeId, DateOnly fromDate)
		{
			await context_.Shifts
				.Where(x => x.EmployeeId == employeeId && x.Date >= fromDate)
				.ExecuteDeleteAsync();
		}

	}
}