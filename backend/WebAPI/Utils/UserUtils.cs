using Easy_Password_Validator;
using Five68.Models;
using Microsoft.Extensions.Options;
namespace Five68.Utils;


public class UserUtils
{
	private readonly CryptoSettings _crypto;
	private readonly PasswordValidatorService _passwordValidatorService;

	public UserUtils(IOptions<AppSettings> options, PasswordValidatorService passwordValidatorService)
	{
		_crypto = options.Value.Crypto;
		_passwordValidatorService = passwordValidatorService;
	}

	public bool CheckPassword(User user, string password)
	{
		return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
	}

	/// <summary>
	/// Validates the password against complexity rules and hashes it.
	/// </summary>
	/// <param name="password">The plain text password.</param>
	/// <returns>The hashed password (contains the salt inside).</returns>
	public string HashAndCheckPassword(string password)
	{
		if (!_passwordValidatorService.TestAndScore(password))
		{
			throw new ArgumentException("Password does not meet the requirements");
		}

		// WorkFactor 12 is a good balance between security and speed.
		return BCrypt.Net.BCrypt.HashPassword(password, _crypto.WorkFactor);
	}
}