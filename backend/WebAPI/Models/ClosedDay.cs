namespace Five68.Models
{
	public class ClosedDay
	{
		public required Guid Id { get; set; }
		public required DateOnly Date { get; set; }
		public Guid? CreatedBy { get; set; }
		public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
		public User Creator { get; set; } = null!;
	}
}