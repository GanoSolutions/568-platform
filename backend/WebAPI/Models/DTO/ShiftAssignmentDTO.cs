namespace Five68.Models.DTO
{
	public class ShiftAssignmentDTO
	{
		public Guid Id { get; set; }
		public DateOnly Date { get; set; }
		public Guid EmployeeId { get; set; }
		public TimeOnly StartTime { get; set; }
		public TimeOnly EndTime { get; set; }
		public Guid CreatedBy { get; set; }
		public DateTimeOffset CreatedAt { get; set; }

		public static ShiftAssignmentDTO FromShiftAssignment(ShiftAssignment sa)
		{
			if (sa is null) return null;

			return new ShiftAssignmentDTO
			{
				Id = sa.Id,
				Date = sa.Date,
				EmployeeId = sa.EmployeeId,
				StartTime = sa.StartTime,
				EndTime = sa.EndTime,
				CreatedBy = sa.CreatedBy,
				CreatedAt = sa.CreatedAt,
			};

		}
	}
}