using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO
{
	public class ShiftAssignmentCreate
	{
		[Required]
		public Guid EmployeeId { get; set; }
		[Required]
		[DataType(DataType.Date)]
		public DateOnly Date { get; set; }
		[Required]
		[DataType(DataType.Time)]
		public TimeOnly StartTime { get; set; }
		[Required]
		[DataType(DataType.Duration)]
		[Range(typeof(TimeSpan), "00:00:00", "1.00:00:00", MinimumIsExclusive = true)]
		public TimeSpan Duration { get; set; }
	}

	public class ShiftAssignmentUpdate
	{
		[Required]
		[DataType(DataType.Time)]
		public TimeOnly StartTime { get; set; }
		[Required]
		[DataType(DataType.Duration)]
		[Range(typeof(TimeSpan), "00:00:00", "1.00:00:00", MinimumIsExclusive = true)]
		public TimeSpan Duration { get; set; }
	}
}