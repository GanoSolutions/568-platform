namespace Five68.Services
{
	public interface INotificationService
	{
		Task SendInviteAsync(string toEmail, string inviteLink);
	}
}
