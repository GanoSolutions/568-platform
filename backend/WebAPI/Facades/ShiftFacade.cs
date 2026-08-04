using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades;

public class ShiftFacade
{
	private readonly Five68DbContext _context;

	public ShiftFacade(Five68DbContext context)
	{
		_context = context;
	}

	internal async Task<Shift> CreateAsync(Shift shift)
	{
		await _context.Shifts.AddAsync(shift);
		await _context.SaveChangesAsync();
		return shift;
	}

	internal async Task<Shift> UpdateAsync(Shift shift)
	{
		shift.UpdatedAt = DateTime.UtcNow;
		_context.Shifts.Update(shift);
		await _context.SaveChangesAsync();
		return shift;
	}

	internal async Task DeleteAsync(Shift shift)
	{
		_context.Shifts.Remove(shift);
		await _context.SaveChangesAsync();
	}

	internal async Task<Shift?> FindByIdAsync(Guid id)
	{
		return await _context.Shifts
			.FirstOrDefaultAsync(x => x.Id == id);
	}

	internal async Task<Shift?> FindByDateAndEmployeeAsync(DateOnly date, Guid employeeId)
	{
		return await _context.Shifts
			.FirstOrDefaultAsync(x => x.Date == date && x.EmployeeId == employeeId);
	}

	internal async Task<IEnumerable<Shift>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate)
	{
		return await _context.Shifts
			.Where(x => x.Date >= startDate && x.Date <= endDate)
			.OrderBy(x => x.Date)
			.ToListAsync();
	}

	internal async Task<bool> IsClosedDayAsync(DateOnly date)
	{
		return await _context.ClosedDays.AnyAsync(x => x.Date == date);
	}

}