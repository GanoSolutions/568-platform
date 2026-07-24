using Five68.Models;

namespace Five68.Services
{
	public interface INotificationService
	{
		Task SendInviteAsync(string toEmail, string inviteLink);
		Task NotifySwapRequestCreatedAsync(SwapRequest request);
		Task NotifySwapRequestRejectedAsync(SwapRequest request);
		Task NotifySwapRequestCancelledAsync(SwapRequest request);
	}
}
