namespace Five68.Models
{
	public class Employee
	{
		public required Guid UserId { get; set; } // PK and FK on Profile
		public string FiscalCode { get; set; } = null!;
		public string Phone { get; set; } = null!;
		public DateOnly? ContractEnd { get; set; }
		public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
		public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
		public User User { get; set; } = null!;
	}
}