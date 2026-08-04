using Five68.Models.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades;

public class RefreshTokenFacade
{
	private readonly Five68DbContext _context;

	public RefreshTokenFacade(Five68DbContext context)
	{
		_context = context;
	}

	public async Task UpsertUserRefreshTokens(UserRefreshTokens refreshTokens)
	{
		await DeleteUserRefreshTokens(refreshTokens.Email);
		_context.RefreshTokens.Add(refreshTokens);
		await _context.SaveChangesAsync();
	}

	public async Task<UserRefreshTokens?> ConsumeRefreshToken(string email)
	{
		UserRefreshTokens? token = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Email == email);
		if (token is null)
		{
			return null;
		}

		_context.RefreshTokens.Remove(token);
		await _context.SaveChangesAsync();
		return token;
	}

	public async Task DeleteUserRefreshTokens(string email)
	{
		await _context.RefreshTokens
			.Where(x => x.Email == email)
			.ExecuteDeleteAsync();
	}
}