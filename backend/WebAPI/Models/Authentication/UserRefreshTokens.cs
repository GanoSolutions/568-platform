namespace Five68.Models.Authentication
{
    public class UserRefreshTokens
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpirationDate { get; set; }
    }
}