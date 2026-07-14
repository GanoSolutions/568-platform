namespace Five68.Models.DTO
{
    public class UserDTO
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public EmployeeDTO? Employee { get; set; }

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
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                Employee = EmployeeDTO.FromEmployee(user.Employee)
            };
        }
    }
}