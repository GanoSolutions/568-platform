using Easy_Password_Validator.Models;

namespace Five68;

public class AppSettings
{
	public const string Position = "AppSettings";

	public CryptoSettings Crypto { get; set; } = new();
	public PasswordRequirements PasswordRequirements { get; set; } = new();
	public JWTSettings JWTSettings { get; set; } = new();
}

public class CryptoSettings
{
	public int WorkFactor { get; init; }
}

public class JWTSettings
{
	public string Secret { get; init; } = string.Empty;
	public double ExpiryMinutes { get; init; }
	public string ValidIssuer { get; init; } = string.Empty;
	public string ValidAudience { get; init; } = string.Empty;
	public bool ValidateIssuer { get; init; }
	public bool ValidateAudience { get; init; }
}

public class TelegramSettings
{
	public bool Enabled { get; init; }
	public string BotToken { get; init; } = string.Empty;
	public List<string> ChatIds { get; init; } = [];
}