namespace Five68.Models
{
    public class ShiftAssignment
    {
        public required Guid Id { get; set; }
        public required Guid ShiftId { get; set; }
        public required Guid EmployeeId { get; set; }
        public bool IsPartial { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Shift Shift { get; set; } = null!;
        public Employee Employee { get; set; } = null!;
    }
}