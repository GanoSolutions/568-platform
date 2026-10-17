using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO;

/// <summary>Request body to create a new shift.</summary>
public class ShiftCreate
{
	/// <summary>The employee to assign.</summary>
	[Required]
	public Guid EmployeeId { get; set; }
	/// <summary>The date of the shift.</summary>
	[Required]
	[DataType(DataType.Date)]
	public DateOnly Date { get; set; }
	/// <summary>When the shift starts.</summary>
	[Required]
	[DataType(DataType.Time)]
	public TimeOnly StartTime { get; set; }
	/// <summary>How long the shift lasts.</summary>
	[Required]
	[DataType(DataType.Duration)]
	[Range(typeof(TimeSpan), "00:00:00", "1.00:00:00", MinimumIsExclusive = true)]
	public TimeSpan Duration { get; set; }
}

/// <summary>Request body to update an existing shift's time/duration.</summary>
public class ShiftUpdate
{
	/// <summary>When the shift starts.</summary>
	[Required]
	[DataType(DataType.Time)]
	public TimeOnly StartTime { get; set; }
	/// <summary>How long the shift lasts.</summary>
	[Required]
	[DataType(DataType.Duration)]
	[Range(typeof(TimeSpan), "00:00:00", "1.00:00:00", MinimumIsExclusive = true)]
	public TimeSpan Duration { get; set; }
}