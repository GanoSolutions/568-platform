namespace Five68.Models.DTO;

public class ShiftDTO
{
	public Guid Id { get; set; }
	public DateOnly Date { get; set; }
	public Guid EmployeeId { get; set; }
	public TimeOnly StartTime { get; set; }
	public TimeSpan Duration { get; set; }
	public Guid CreatedBy { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }

	public static ShiftDTO FromShift(Shift shift)
	{
		if (shift is null) return null;

		return new ShiftDTO
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