using Five68.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Five68.Hubs
{
	public record ShiftChangedEvent(DateOnly Date);
	public record SwapRequestChangedEvent(Guid RequesterId, SwapRequestStatus Status, DateOnly ShiftDate);

	public interface IAppRealtimeClient
	{
		Task SwapRequestsChanged(SwapRequestChangedEvent payload);
		Task ShiftsChanged(ShiftChangedEvent payload);
	}

	[Authorize]
	public class AppHub : Hub<IAppRealtimeClient>
	{

	}
}