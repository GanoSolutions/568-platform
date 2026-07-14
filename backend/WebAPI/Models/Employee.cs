namespace Five68.Models
{
	public class Employee
	{
		public required Guid UserId { get; set; } // PK and FK on Profile
		public required string Name { get; set; }
		public required string Surname { get; set; }
		public required string FiscalCode { get; set; }
		public required string Phone { get; set; }
		public string? Color { get; set; }
		public DateOnly? ContractEnd { get; set; }
		public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
		public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
		public User User { get; set; } = null!;
	}
}