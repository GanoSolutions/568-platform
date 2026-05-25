namespace Five68.Models
{
    public class SwapRequest
    {
        public required Guid Id { get; set; }
        public required Guid ShiftId { get; set; }
        public required Guid RequesterId { get; set; } // FK to Profile
        public required Guid TargetEmployeeId { get; set; } // FK to Profile
        public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? RespondedAt { get; set; }

        public Shift Shift { get; set; } = null!;
        public User Requester { get; set; } = null!;
        public User TargetEmployee { get; set; } = null!;
    }
}