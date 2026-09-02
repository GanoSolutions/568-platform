using Five68.Models.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class RefreshTokenFacade
	{
		private readonly Five68DbContext db_;

		public RefreshTokenFacade(Five68DbContext db)
		{
			db_ = db;
		}

		public async Task UpsertUserRefreshTokens(UserRefreshTokens refreshTokens)
		{
			await DeleteUserRefreshTokens(refreshTokens.UserId);
			db_.RefreshTokens.Add(refreshTokens);
			await db_.SaveChangesAsync();
		}

		public async Task<UserRefreshTokens?> ConsumeRefreshToken(Guid userId)
		{
			UserRefreshTokens token = await db_.RefreshTokens.FirstOrDefaultAsync(x => x.UserId == userId);
			if (token is null)
			{
				return null;
			}

			db_.RefreshTokens.Remove(token);
			await db_.SaveChangesAsync();
			return token;
		}

		public async Task DeleteUserRefreshTokens(Guid userId)
		{
			await db_.RefreshTokens
				.Where(x => x.UserId == userId)
				.ExecuteDeleteAsync();
		}
	}
}