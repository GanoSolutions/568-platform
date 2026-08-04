using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades;

public class UserFacade
{
	private readonly Five68DbContext _context;

	public UserFacade(Five68DbContext context)
	{
		_context = context;
	}

	internal async Task CreateAsync(User user)
	{
		await _context.Users.AddAsync(user);
		await _context.SaveChangesAsync();
	}

	internal async Task UpdateAsync(User user)
	{
		user.UpdatedAt = DateTime.UtcNow;

		_context.Users.Update(user);
		await _context.SaveChangesAsync();
	}

	internal async Task<User?> FindByEmailAsync(string email)
	{
		return await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
	}

	internal async Task<User?> FindByIdAsync(Guid id)
	{
		return await _context.Users
			.Include(x => x.Employee)
			.FirstOrDefaultAsync(x => x.Id == id);
	}

	internal async Task<IEnumerable<User>> GetAll()
	{
		return await _context.Users
			.Include(x => x.Employee)
			.ToListAsync();
	}

	internal async Task<User?> FindByInviteTokenAsync(string token)
	{
		return await _context.Users.FirstOrDefaultAsync(x => x.InviteToken == token);
	}

	internal async Task<int> GetUserNumber()
	{
		return await _context.Users.CountAsync();
	}

}