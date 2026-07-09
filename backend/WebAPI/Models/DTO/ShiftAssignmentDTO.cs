namespace Five68.Models.DTO
{
	public class ShiftAssignmentDTO
	{
		public Guid Id { get; set; }
		public DateOnly Date { get; set; }
		public Guid EmployeeId { get; set; }
		public EmployeeDTO Employee { get; set; }
		public TimeOnly StartTime { get; set; }
		public TimeOnly EndTime { get; set; }
		public DateTimeOffset CreatedAt { get; set; }

		public static ShiftAssignmentDTO FromShiftAssignment(ShiftAssignment sa)
		{
			if (sa is null) return null;

			return new ShiftAssignmentDTO
			{
				Id = sa.Id,
				Date = sa.Date,
				EmployeeId = sa.EmployeeId,
				Employee = EmployeeDTO.FromEmployee(sa.Employee),
				StartTime = sa.StartTime,
				EndTime = sa.EndTime,
				CreatedAt = sa.CreatedAt
			};

		}
	}
}