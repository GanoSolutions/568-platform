using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Five68.Hubs
{
	public record ShiftChangedEvent(DateOnly Date);

	public interface IAppRealtimeClient
	{
		Task SwapRequestsChanged();
		Task ShiftsChanged(ShiftChangedEvent payload);
	}

	[Authorize]
	public class AppHub : Hub<IAppRealtimeClient>
	{

	}
}