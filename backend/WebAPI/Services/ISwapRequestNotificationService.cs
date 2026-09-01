using Five68.Hubs;
using Five68.Models;

namespace Five68.Services
{
	public interface ISwapRequestNotificationService
	{
		Task NotifySwapRequestChangedAsync(SwapRequestChangedEvent payload);
	}
}
