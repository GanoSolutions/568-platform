using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Claims;

namespace Five68.Services
{
	public class AuthService
	{
		private readonly UserService userService_;
		private readonly JwtService jwtService_;
		private readonly RefreshTokenFacade refreshTokenFacade_;
		private readonly ILogger<AuthService> logger_;

		public AuthService(UserService userService, JwtService jwtService, RefreshTokenFacade refreshTokenFacade, ILogger<AuthService> logger)
		{
			userService_ = userService;
			jwtService_ = jwtService;
			refreshTokenFacade_ = refreshTokenFacade;
			logger_ = logger;
		}

		public async Task<Tokens> Login(UserLogin userData)
		{
			(bool validUser, User? user) = await userService_.TryGetUserAndCheckPasswordAsync(userData);

			if (!validUser || user is null)
			{
				throw new UnauthorizedException("Credenziali non valide");
			}

			if (user.Status != UserStatus.Active)
			{
				throw new UnauthorizedException("Account non attivo");
			}

			Tokens token = jwtService_.GenerateTokens(user.Id, user.Email) ?? throw new InternalServerErrorException("Tentativo non valido");

			await refreshTokenFacade_.UpsertUserRefreshTokens(new UserRefreshTokens
			{
				RefreshToken = token.RefreshToken,
				UserId = user.Id,
				ExpirationDate = DateTime.UtcNow + TimeSpan.FromDays(1)
			});

			logger_.LogInformation($"User {user.Id} ({user.Email}) has logged in");

			return token;
		}

		public async Task<Tokens> Refresh(Tokens token)
		{
			ClaimsPrincipal principal = jwtService_.GetPrincipalFromExpiredToken(token.AccessToken);
			string? email = principal.FindFirst(ClaimTypes.Email)?.Value;
			string? userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (email is null || userId is null || !Guid.TryParse(userId, out Guid id))
			{
				throw new UnauthorizedException("Token non valido");
			}

			UserRefreshTokens? savedRefreshToken = await refreshTokenFacade_.ConsumeRefreshToken(id);
			if (savedRefreshToken is null || savedRefreshToken.RefreshToken != token.RefreshToken || savedRefreshToken.ExpirationDate < DateTime.UtcNow)
			{
				throw new UnauthorizedException("Refresh token non valido o scaduto");
			}

			Tokens newTokens = jwtService_.GenerateTokens(id, email) ?? throw new UnauthorizedException("Tentativo non valido");
			await refreshTokenFacade_.UpsertUserRefreshTokens(new UserRefreshTokens
			{
				RefreshToken = newTokens.RefreshToken,
				UserId = id,
				ExpirationDate = DateTime.UtcNow.AddDays(1),
			});

			return newTokens;
		}

		public async Task Logout(Guid userId)
		{
			await refreshTokenFacade_.DeleteUserRefreshTokens(userId);
		}

	}
}