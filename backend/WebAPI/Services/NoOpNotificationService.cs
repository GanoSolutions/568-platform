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
		_logger.LogInformation($"Swap request {request.Id} cancelled");
		return Task.CompletedTask;
	}

	public Task NotifySwapRequestCreatedAsync(SwapRequest request)
	{
		_logger.LogInformation($"Swap request {request.Id} created: shift: {request.ShiftId}, requester: {request.RequesterId} for {request.TargetEmployeeId}");
		return Task.CompletedTask;
	}

	public Task NotifySwapRequestRespondedAsync(SwapRequest request)
	{
		_logger.LogInformation($"Swap request {request.Id} responded with status {request.Status}");
		return Task.CompletedTask;
	}


	public Task SendInviteAsync(string toEmail, string inviteLink)
	{
		_logger.LogInformation($"Invite for {toEmail} — link: {inviteLink}");
		return Task.CompletedTask;
	}
}