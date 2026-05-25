namespace Five68.Models.DTO
{
    public class UserDTO
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string? Color { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public static UserDTO FromUser(User user)
        {
            if (user is null)
            {
                return null;
            }

            return new UserDTO
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                Status = user.Status,
                Color = user.Color,
                CreatedAt = user.CreatedAt
            };
        }
    }
}