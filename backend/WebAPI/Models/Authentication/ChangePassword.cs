namespace Five68.Models.Authentication
{
	public class ChangePassword
	{
		public required string CurrentPassword { get; set; }
		public required string NewPassword { get; set; }
	}
}