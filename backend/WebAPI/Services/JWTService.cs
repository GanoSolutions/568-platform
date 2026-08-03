using Five68.Exceptions;
using Five68.Models.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Five68.Services;

public class JwtService
{
	private readonly JWTSettings _jwtSettings;
	private readonly ILogger _logger;

	public JwtService(IOptions<AppSettings> appsettings, ILogger<JwtService> logger)
	{
		_jwtSettings = appsettings.Value.JWTSettings;
		_logger = logger;

		if (Encoding.UTF8.GetBytes(_jwtSettings.Secret).Length < 32)
		{
			throw new InvalidOperationException("JWT secret must be at least 32 bytes");
		}
	}

	public Tokens GenerateTokens(Guid id, string email)
	{
		List<Claim> claims =
		[
			new Claim(ClaimTypes.Email, email),
			new Claim(ClaimTypes.NameIdentifier, id.ToString())
		];

		return GenerateJWTTokens(new ClaimsIdentity(claims));
	}

	private Tokens GenerateJWTTokens(ClaimsIdentity identity)
	{
		try
		{
			byte[] tokenKey = Encoding.UTF8.GetBytes(_jwtSettings.Secret);
			JwtSecurityTokenHandler tokenHandler = new();
			SecurityTokenDescriptor tokenDescriptor = new()
			{
				Subject = identity,
				Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
				Issuer = _jwtSettings.ValidIssuer,
				Audience = _jwtSettings.ValidAudience,
				SigningCredentials = new SigningCredentials(
					new SymmetricSecurityKey(tokenKey),
					SecurityAlgorithms.HmacSha256Signature)
			};

			SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
			string refreshToken = GenerateRefreshToken();
			return new Tokens { AccessToken = tokenHandler.WriteToken(token), RefreshToken = refreshToken };
		}
		catch (Exception e)
		{
			_logger.LogCritical(e, "Unable to generate a JWT Token");
			throw;
		}
	}

	private static string GenerateRefreshToken()
	{
		return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
	}

	public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
	{
		byte[] key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

		TokenValidationParameters tokenValidationParameters = new()
		{
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateLifetime = false,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(key),
			ClockSkew = TimeSpan.Zero
		};

		JwtSecurityTokenHandler tokenHandler = new();
		try
		{
			ClaimsPrincipal principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
			return securityToken is not JwtSecurityToken jwtToken || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase)
				? throw new UnauthorizedException("Token non valido")
				: principal;
		}
		catch (SecurityTokenException ex)
		{
			_logger.LogWarning(ex, "");
			throw new UnauthorizedException("Token non valido");
		}
		catch (SecurityTokenArgumentException ex)
		{
			_logger.LogWarning(ex, "");
			throw new UnauthorizedException("Token non valido");
		}
	}
}