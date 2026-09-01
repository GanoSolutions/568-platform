namespace Five68.Services
{
	public interface IInviteService
	{
		Task SendInviteAsync(string toEmail, string inviteLink);
	}
}