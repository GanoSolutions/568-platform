namespace Five68.Services
{
	public class NoOpInviteService : IInviteService
	{
		private readonly ILogger<NoOpInviteService> _logger;

		public NoOpInviteService(ILogger<NoOpInviteService> logger)
		{
			_logger = logger;
		}

		public Task SendInviteAsync(string toEmail, string inviteLink)
		{
			_logger.LogInformation($"Invite {inviteLink} sent to {toEmail}!");
			return Task.CompletedTask;
		}
	}
}