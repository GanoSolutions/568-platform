namespace Five68.Models.Authentication
{
    public class UserRefreshTokens
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpirationDate { get; set; }
    }
}