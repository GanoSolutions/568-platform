namespace Five68.Services
{
	public class NoOpNotificationService : INotificationService
	{
		private readonly ILogger<NoOpNotificationService> logger_;

		public NoOpNotificationService(ILogger<NoOpNotificationService> logger)
		{
			logger_ = logger;
		}

		public Task SendInviteAsync(string toEmail, string inviteLink)
		{
			logger_.LogInformation("Invite for {Email} — link: {Link}", toEmail, inviteLink);
			return Task.CompletedTask;
		}
	}
}
