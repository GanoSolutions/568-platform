using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Five68.Hubs
{
	public interface IAppRealtimeClient
	{
		Task SwapRequestsChanged();
		Task ShiftsChanged();
	}

	[Authorize]
	public class AppHub : Hub<IAppRealtimeClient>
	{

	}
}