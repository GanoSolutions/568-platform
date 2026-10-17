namespace Five68.Models.Authentication;

/// <summary>Request body to change the authenticated user's password.</summary>
public class ChangePassword
{
	/// <summary>The current password, for verification.</summary>
	public required string CurrentPassword { get; set; }
	/// <summary>The new password to set.</summary>
	public required string NewPassword { get; set; }
}