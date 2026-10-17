using Five68.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Five68.Services;

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

	public async Task NotifyShiftChangedAsync(DateOnly shiftDate)
	{
		_logger.LogInformation("Broadcasting ShiftsChanged event for {ShiftDate}", shiftDate);
		await _hub.Clients.All.ShiftsChanged(new ShiftChangedEvent(shiftDate));
	}

	public async Task NotifySwapRequestChangedAsync(SwapRequestChangedEvent payload)
	{
		_logger.LogInformation("Broadcasting SwapRequestsChanged event for requester {RequesterId}, status {Status}", payload.RequesterId, payload.Status);
		await _hub.Clients.All.SwapRequestsChanged(payload);
	}
}