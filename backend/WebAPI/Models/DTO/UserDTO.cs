namespace Five68.Models.DTO;

/// <summary>A user account.</summary>
public class UserDTO
{
	/// <summary>Unique identifier.</summary>
	public Guid Id { get; set; }
	/// <summary>Login email.</summary>
	public required string Email { get; set; }
	/// <summary>Permission level.</summary>
	public UserRole Role { get; set; }
	/// <summary>Account status (e.g. pending invite, active, disabled).</summary>
	public UserStatus Status { get; set; }
	/// <summary>The employee profile linked to this account, if any.</summary>
	public EmployeeDTO? Employee { get; set; }

	/// <summary>When the account was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Maps a <see cref="User"/> entity to its DTO.</summary>
	public static UserDTO? FromUser(User? user)
	{
		return user is null
			? null
			: new UserDTO
			{
				Id = user.Id,
				Email = user.Email,
				Role = user.Role,
				Status = user.Status,
				CreatedAt = user.CreatedAt,
				Employee = EmployeeDTO.FromEmployee(user.Employee)
			};
	}
}