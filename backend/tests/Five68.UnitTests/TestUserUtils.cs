using Easy_Password_Validator;
using Easy_Password_Validator.Models;
using Five68.Models;
using Five68.Utils;
using FluentAssertions;
using Microsoft.Extensions.Options;
namespace Five68.UnitTests;

public class TestUserUtils
{
	private readonly UserUtils sut_;

	public TestUserUtils()
	{
		IOptions<AppSettings> settings = Options.Create(new AppSettings
		{
			Crypto = new CryptoSettings { WorkFactor = 4 },
			PasswordRequirements = new PasswordRequirements
			{
				MinLength = 8,
				RequireUppercase = true,
				RequireDigit = true,
				RequirePunctuation = true
			}
		});

		PasswordValidatorService validator = new PasswordValidatorService(settings.Value.PasswordRequirements);
		sut_ = new UserUtils(settings, validator);
	}

	// --- HashAndCheckPassword ---

	[Fact]
	public void HashAndCheckPassword_ValidPassword_ReturnsHashVerifiableByBCrypt()
	{
		string password = "ValidP@ss1!";

		string hash = sut_.HashAndCheckPassword(password);

		BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
	}

	[Fact]
	public void HashAndCheckPassword_SamePasswordTwice_ReturnsDifferentHashes()
	{
		// BCrypt generates different salt each time
		string hash1 = sut_.HashAndCheckPassword("ValidP@ss1!");
		string hash2 = sut_.HashAndCheckPassword("ValidP@ss1!");

		hash1.Should().NotBe(hash2);
	}

	[Fact]
	public void HashAndCheckPassword_WeakPassword_ThrowsArgumentException()
	{
		Action act = () => sut_.HashAndCheckPassword("1234");

		act.Should().Throw<ArgumentException>();
	}

	// --- CheckPassword ---

	[Fact]
	public void CheckPassword_CorrectPassword_ReturnsTrue()
	{
		string password = "ValidP@ss1!";
		User user = new User
		{
			Id = Guid.NewGuid(),
			Email = "test@five68.com",
			Role = UserRole.Employee,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4)
		};

		sut_.CheckPassword(user, password).Should().BeTrue();
	}

	[Fact]
	public void CheckPassword_WrongPassword_ReturnsFalse()
	{
		User user = new()
		{
			Id = Guid.NewGuid(),
			Email = "test@five68.com",
			Role = UserRole.Employee,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct@1!", workFactor: 4)
		};

		sut_.CheckPassword(user, "Wrong@1!").Should().BeFalse();
	}

	[Fact]
	public void CheckPassword_NullHash_ThrowsSaltParseException()
	{
		User user = new()
		{
			Id = Guid.NewGuid(),
			Email = "test@five68.com",
			Role = UserRole.Employee,
			PasswordHash = null  // utente Pending, non ha ancora settato la password
		};

		Action act = () => sut_.CheckPassword(user, "AnyP@ss1!");

		// BCrypt.Verify esplode se l'hash è null — comportamento atteso
		act.Should().Throw<Exception>();
	}
}