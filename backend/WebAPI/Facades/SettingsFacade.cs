using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class SettingsFacade
	{
		private readonly Five68DbContext _context;
		public SettingsFacade(Five68DbContext context)
		{
			_context = context;
		}

		internal async Task<IEnumerable<ClosedDay>> GetClosedDaysByDateRangeAsync(DateOnly start, DateOnly end)
		{
			return await _context.ClosedDays
				.Where(x => x.Date >= start && x.Date <= end)
				.OrderBy(x => x.Date)
				.ToListAsync();
		}
	}
}