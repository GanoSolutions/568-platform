namespace Five68.Models
{
    public class Shift
    {
        public required Guid Id { get; set; }
        public required DateTimeOffset WorkDate { get; set; }
        public bool IsClosed { get; set; } = false;
        public Guid? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User Creator { get; set; } = null!;
        public ICollection<ShiftAssignment> Assignments { get; set; } = [];
        public ICollection<SwapRequest> SwapRequests { get; set; } = [];
    }
}