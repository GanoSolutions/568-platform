namespace Five68.Models
{
    public class User
    {
        public required Guid Id { get; set; }
        public required string Email { get; set; }
        public string? PasswordHash { get; set; }
        public UserStatus Status { get; set; }
        public required string FullName { get; set; }
        public required UserRole Role { get; set; }
        public string? Color { get; set; }
        public string? InviteToken { get; set; }
        public DateTimeOffset? InviteTokenExpiry { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public Employee? Employee { get; set; }
    }
}