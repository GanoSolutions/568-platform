using Five68.Models.Authentication;
using Five68.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Five68.UnitTests;

public class JwtServiceTests
{
	private readonly JwtService _sut;

	// Costanti condivise tra generazione e verifica
	private const string Secret = "super-secret-key-at-least-32-bytes!!";
	private const string Issuer = "five68-test";
	private const string Audience = "five68-client";

	public JwtServiceTests()
	{
		IOptions<AppSettings> settings = Options.Create(new AppSettings
		{
			JWTSettings = new JWTSettings
			{
				Secret = Secret,
				ExpiryMinutes = 60,
				ValidIssuer = Issuer,
				ValidAudience = Audience
			}
		});

		ILogger<JwtService> logger = Substitute.For<ILogger<JwtService>>();
		_sut = new JwtService(settings, logger);
	}

	[Fact]
	public void GenerateTokens_ValidInput_ReturnsBothTokens()
	{
		Tokens tokens = _sut.GenerateTokens(Guid.NewGuid(), "admin@five68.com");

		tokens.AccessToken.Should().NotBeNullOrEmpty();
		tokens.RefreshToken.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void GenerateTokens_AccessToken_ContainsCorrectEmailClaim()
	{
		string email = "admin@five68.com";
		Tokens tokens = _sut.GenerateTokens(Guid.NewGuid(), email);

		JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
		JwtSecurityToken jwt = handler.ReadJwtToken(tokens.AccessToken);

		jwt.Claims
			.First(c => c.Type == JwtRegisteredClaimNames.Email)
			.Value.Should().Be(email);
	}

	[Fact]
	public void GenerateTokens_AccessToken_IsValidSignature()
	{
		Tokens tokens = _sut.GenerateTokens(Guid.NewGuid(), "admin@five68.com");

		JwtSecurityTokenHandler handler = new();
		TokenValidationParameters validationParams = new()
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
			ValidateIssuer = true,
			ValidIssuer = Issuer,
			ValidateAudience = true,
			ValidAudience = Audience,
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero
		};

		Func<ClaimsPrincipal> act = () => handler.ValidateToken(tokens.AccessToken, validationParams, out _);

		act.Should().NotThrow();
	}

	[Fact]
	public void GenerateTokens_TwoCallsSameUser_ReturnDifferentRefreshTokens()
	{
		Guid id = Guid.NewGuid();
		Tokens t1 = _sut.GenerateTokens(id, "admin@five68.com");
		Tokens t2 = _sut.GenerateTokens(id, "admin@five68.com");

		t1.RefreshToken.Should().NotBe(t2.RefreshToken);
	}

	[Fact]
	public void Constructor_SecretTooShort_ThrowsInvalidOperationException()
	{
		IOptions<AppSettings> badSettings = Options.Create(new AppSettings
		{
			JWTSettings = new JWTSettings { Secret = "short", ExpiryMinutes = 60 }
		});

		Action act = () => new JwtService(badSettings, Substitute.For<ILogger<JwtService>>());

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*32 bytes*");
	}
}