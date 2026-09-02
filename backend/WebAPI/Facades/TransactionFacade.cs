using Microsoft.EntityFrameworkCore.Storage;

namespace Five68.Facades
{
	public class TransactionFacade
	{
		private readonly Five68DbContext _context;

		public TransactionFacade(Five68DbContext context)
		{
			_context = context;
		}

		internal async Task ExecuteAsync(Func<Task> action)
		{
			await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

			try
			{
				await action();
				await transaction.CommitAsync();
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

	}
}