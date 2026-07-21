using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;

namespace Five68.Services
{
	public class SettingsService
	{
		private readonly SettingsFacade _settingsFacade;

		public SettingsService(SettingsFacade settingsFacade)
		{
			_settingsFacade = settingsFacade;
		}

		public async Task<IEnumerable<ClosedDayDTO>> GetClosedDaysByDateRange(DateOnly start, DateOnly end)
		{
			if (end < start)
			{
				throw new EntityException("La data di fine deve essere successiva alla data di inizio");
			}

			return (await _settingsFacade.GetClosedDaysByDateRangeAsync(start, end)).Select(ClosedDayDTO.FromClosedDay);
		}
	}
}