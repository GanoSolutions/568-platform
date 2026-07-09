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
		[DataType(DataType.Time)]
		public TimeOnly EndTime { get; set; }
	}

	public class ShiftAssignmentUpdate
	{
		[Required]
		[DataType(DataType.Time)]
		public TimeOnly StartTime { get; set; }
		[Required]
		[DataType(DataType.Time)]
		public TimeOnly EndTime { get; set; }
	}
}