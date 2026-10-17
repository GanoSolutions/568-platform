using Five68.Models;
using Five68.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

	internal async Task DeleteFutureAssignmentsForEmployeeAsync(Guid employeeId, DateOnly fromDate)
	{
		await _context.Shifts
			.Where(x => x.EmployeeId == employeeId && x.Date >= fromDate)
			.ExecuteDeleteAsync();
	}

	internal async Task<ShiftCopyWeekResult?> CopyWeekAsync(DateOnly sourceWeekMonday, DateOnly targetStartDate, DateOnly targetEndDate, Guid requesterId)
	{
		List<Shift> sourceShifts = await _context.Shifts
			.Where(x => x.Date >= sourceWeekMonday && x.Date <= sourceWeekMonday.AddDays(6))
			.ToListAsync();

		if (sourceShifts.Count == 0)
		{
			return null;
		}

		await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();
		ShiftCopyWeekResult outcome = new();
		List<Shift> toCreate = new();

		for (DateOnly targetMonday = targetStartDate; targetMonday <= targetEndDate; targetMonday = targetMonday.AddDays(7))
		{
			foreach (Shift source in sourceShifts)
			{
				int offset = source.Date.DayNumber - sourceWeekMonday.DayNumber;
				DateOnly targetDate = targetMonday.AddDays(offset);
				if (targetDate == source.Date)
				{
					continue;
				}

				if (await _context.ClosedDays.AnyAsync(x => x.Date == targetDate))
				{
					outcome.SkippedClosedDays++;
					continue;
				}

				bool existing = await _context.Shifts.AnyAsync(x => x.Date == targetDate && x.EmployeeId == source.EmployeeId);
				if (existing)
				{
					await _context.Shifts
						.Where(x => x.Date == targetDate && x.EmployeeId == source.EmployeeId)
						.ExecuteDeleteAsync();
					outcome.Overwritten++;
				}
				else
				{
					outcome.Created++;
				}

				toCreate.Add(new Shift
				{
					Id = Guid.NewGuid(),
					Date = targetDate,
					EmployeeId = source.EmployeeId,
					StartTime = source.StartTime,
					Duration = source.Duration,
					CreatedBy = requesterId,
				});
			}
		}

		await _context.Shifts.AddRangeAsync(toCreate);
		await _context.SaveChangesAsync();
		await transaction.CommitAsync();
		return outcome;
	}
}