using Five68.Models;

namespace Five68.Services;

public class NoOpNotificationService : INotificationService
{
	private readonly ILogger<NoOpNotificationService> _logger;

	public NoOpNotificationService(ILogger<NoOpNotificationService> logger)
	{
		_logger = logger;
	}

	public Task NotifySwapRequestCancelledAsync(SwapRequest request)
	{
		_logger.LogInformation("Swap request {SwapRequestId} cancelled", request.Id);
		return Task.CompletedTask;
	}

	public Task NotifySwapRequestCreatedAsync(SwapRequest request)
	{
		_logger.LogInformation("Swap request {SwapRequestId} created: shift: {ShiftId}, requester: {RequesterId} for {TargetEmployeeId}", request.Id, request.ShiftId, request.RequesterId, request.TargetEmployeeId);
		return Task.CompletedTask;
	}

	public Task NotifySwapRequestRespondedAsync(SwapRequest request)
	{
		_logger.LogInformation("Swap request {SwapRequestId} responded with status {Status}", request.Id, request.Status);
		return Task.CompletedTask;
	}


	public Task SendInviteAsync(string toEmail, string inviteLink)
	{
		_logger.LogInformation("Invite for {ToEmail} — link: {InviteLink}", toEmail, inviteLink);
		return Task.CompletedTask;
	}
}