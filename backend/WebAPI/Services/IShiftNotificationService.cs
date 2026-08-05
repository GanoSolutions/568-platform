using Five68.Models;

namespace Five68.Services
{
	public interface IShiftNotificationService
	{
		Task NotifyShiftChangedAsync(DateOnly date);
	}
}