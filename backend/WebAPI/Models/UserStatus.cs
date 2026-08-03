namespace Five68.Models;

public enum UserStatus
{
	Pending,    // Created, invite not send
	Invited,    // invite sent, first login not completed
	Active,     // first login completed
	Disabled
}