namespace Five68.Models.DTO;

/// <summary>A scheduled work shift.</summary>
public class ShiftDTO
{
	/// <summary>Unique identifier.</summary>
	public Guid Id { get; set; }
	/// <summary>The date of the shift.</summary>
	public DateOnly Date { get; set; }
	/// <summary>The employee assigned to the shift.</summary>
	public Guid EmployeeId { get; set; }
	/// <summary>When the shift starts.</summary>
	public TimeOnly StartTime { get; set; }
	/// <summary>How long the shift lasts.</summary>
	public TimeSpan Duration { get; set; }
	/// <summary>The user who created the shift.</summary>
	public Guid CreatedBy { get; set; }
	/// <summary>When the shift was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }
	/// <summary>When the shift was last updated.</summary>
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>Maps a <see cref="Shift"/> entity to its DTO.</summary>
	public static ShiftDTO? FromShift(Shift? shift)
	{
		return shift is null
			? null
			: new ShiftDTO
			{
				Id = shift.Id,
				Date = shift.Date,
				EmployeeId = shift.EmployeeId,
				StartTime = shift.StartTime,
				Duration = shift.Duration,
				CreatedBy = shift.CreatedBy,
				CreatedAt = shift.CreatedAt,
				UpdatedAt = shift.UpdatedAt
			};
	}
}