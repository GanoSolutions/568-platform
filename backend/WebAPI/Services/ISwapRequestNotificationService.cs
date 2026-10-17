using Five68.Hubs;

namespace Five68.Services;

public interface ISwapRequestNotificationService
{
	Task NotifySwapRequestChangedAsync(SwapRequestChangedEvent payload);
}