namespace Five68.Models;

public class Shift
{
	public required Guid Id { get; set; }
	public required DateOnly Date { get; set; }
	public required Guid EmployeeId { get; set; }
	public required TimeOnly StartTime { get; set; }
	public required TimeSpan Duration { get; set; }
	public Guid CreatedBy { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	public User Creator { get; set; } = null!;
	public Employee Employee { get; set; } = null!;
}