using Five68.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
			catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
			{
				await transaction.RollbackAsync();
				throw new EntityException(BuildUniqueViolationMessage(pg));
			}
			catch
			{
				await transaction.RollbackAsync();
				throw;
			}
		}

		private static string BuildUniqueViolationMessage(PostgresException ex)
		{
			string constraint = ex.ConstraintName ?? string.Empty;

			if (constraint.Contains("Email", StringComparison.OrdinalIgnoreCase))
				return "Email già in uso";
			if (constraint.Contains("FiscalCode", StringComparison.OrdinalIgnoreCase))
				return "Codice fiscale già in uso";

			return "Uno dei valori inseriti è già in uso";
		}

	}
}