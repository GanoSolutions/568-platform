using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class UserFacade
	{
		private readonly Five68DbContext context_;

		public UserFacade(Five68DbContext context)
		{
			context_ = context;
		}

		internal async Task CreateAsync(User user)
		{
			await context_.Users.AddAsync(user);
			await context_.SaveChangesAsync();
		}

		internal async Task UpdateAsync(User user)
		{
			user.UpdatedAt = DateTime.UtcNow;

			context_.Users.Update(user);
			await context_.SaveChangesAsync();
		}

		internal async Task<User> FindByEmailAsync(string email)
		{
			return await context_.Users.FirstOrDefaultAsync(x => x.Email == email);
		}

		internal async Task<User> FindByIdAsync(Guid id)
		{
			return await context_.Users.FindAsync(id);
		}

		internal async Task<IEnumerable<User>> GetAll()
		{
			return await context_.Users.ToListAsync();
		}

		internal async Task<User> FindByInviteTokenAsync(string token)
		{
			return await context_.Users.FirstOrDefaultAsync(x => x.InviteToken == token);
		}

		internal async Task<int> GetUserNumber()
		{
			return await context_.Users.CountAsync();
		}

	}
}