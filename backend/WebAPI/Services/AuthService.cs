using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.Authentication;
using System.Security.Claims;

namespace Five68.Services;

public class AuthService
{
	private readonly UserService _userService;
	private readonly JwtService _jwtService;
	private readonly RefreshTokenFacade _refreshTokenFacade;
	private readonly ILogger _logger;

	public AuthService(
		UserService userService,
		JwtService jwtService,
		RefreshTokenFacade refreshTokenFacade,
		ILogger<AuthService> logger
		)
	{
		_userService = userService;
		_jwtService = jwtService;
		_refreshTokenFacade = refreshTokenFacade;
		_logger = logger;
	}

	public async Task<Tokens> Login(UserLogin userData)
	{
		(bool validUser, User? user) = await _userService.TryGetUserAndCheckPasswordAsync(userData);

		if (!validUser || user is null)
		{
			throw new UnauthorizedException("Credenziali non valide");
		}

		if (user.Status != UserStatus.Active)
		{
			throw new UnauthorizedException("Account non attivo");
		}

		Tokens token = _jwtService.GenerateTokens(user.Id, user.Email) ?? throw new InternalServerErrorException("Tentativo non valido");

		await _refreshTokenFacade.UpsertUserRefreshTokens(new UserRefreshTokens
		{
			RefreshToken = token.RefreshToken,
			Email = user.Email,
			ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
		});

		_logger.LogInformation("User {UserId} ({Email}) logged in successfully", user.Id, user.Email);

		return token;
	}

	public async Task<Tokens> Refresh(Tokens token)
	{
		ClaimsPrincipal principal = _jwtService.GetPrincipalFromExpiredToken(token.AccessToken);
		string? email = principal.FindFirst(ClaimTypes.Email)?.Value;
		string? userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (email is null || userId is null || !Guid.TryParse(userId, out Guid id))
		{
			throw new UnauthorizedException("Token non valido");
		}

		UserRefreshTokens? savedRefreshToken = await _refreshTokenFacade.ConsumeRefreshToken(email);
		if (savedRefreshToken is null || savedRefreshToken.RefreshToken != token.RefreshToken || savedRefreshToken.ExpirationDate < DateTime.UtcNow)
		{
			throw new UnauthorizedException("Refresh token non valido o scaduto");
		}

		Tokens newTokens = _jwtService.GenerateTokens(id, email) ?? throw new UnauthorizedException("Tentativo non valido");
		await _refreshTokenFacade.UpsertUserRefreshTokens(new UserRefreshTokens
		{
			RefreshToken = newTokens.RefreshToken,
			Email = email,
			ExpirationDate = DateTime.UtcNow.AddDays(1),
		});

		_logger.LogInformation("Refreshed tokens for user {UserId} ({Email})", id, email);

		return newTokens;
	}

	public async Task Logout(string email)
	{
		await _refreshTokenFacade.DeleteUserRefreshTokens(email);
		_logger.LogInformation("User {Email} logged out", email);
	}

}