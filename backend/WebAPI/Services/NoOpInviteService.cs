namespace Five68.Services
{
	public class NoOpInviteService : IInviteService
	{
		public Task SendInviteAsync(string toEmail, string inviteLink)
		{
			throw new NotImplementedException();
		}
	}
}