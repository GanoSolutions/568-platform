using Five68.Hubs;
using Five68.Models;
using Microsoft.AspNetCore.SignalR;

namespace Five68.Services
{
	public class SignalRNotificationService : ISwapRequestNotificationService, IShiftNotificationService
	{
		private readonly IHubContext<AppHub, IAppRealtimeClient> _hub;
		private readonly ILogger<SignalRNotificationService> _logger;

		public SignalRNotificationService(
			IHubContext<AppHub, IAppRealtimeClient> hub,
			ILogger<SignalRNotificationService> logger
		)
		{
			_hub = hub;
			_logger = logger;
		}

		public async Task NotifyShiftChangedAsync(DateOnly date)
		{
			_logger.LogInformation("Broadcasting ShiftsChanged event for {Date}", date);
			await _hub.Clients.All.ShiftsChanged(new ShiftChangedEvent(date));
		}

		public async Task NotifySwapRequestChangedAsync(SwapRequestChangedEvent payload)
		{
			_logger.LogInformation("Broadcasting SwapRequestsChanged event for requester {RequesterId}, status {Status}", payload.RequesterId, payload.Status);
			await _hub.Clients.All.SwapRequestsChanged(payload);
		}
	}
}