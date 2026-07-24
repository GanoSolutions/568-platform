using Five68.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Win32.SafeHandles;

namespace Five68.Facades
{
	public class SwapRequestFacade
	{

		/// <summary>
		/// Esito di <see cref="TryAcceptAsync"/>: distingue i due motivi di fallimento (per dare
		/// messaggi utente diversi) dal successo, senza che la Facade lanci eccezioni di dominio.
		/// Annidato qui perché è un dettaglio del contratto tra questo metodo e il chiamante, non
		/// un concetto di dominio riutilizzato altrove (a differenza di SwapRequestStatus).
		/// </summary>
		internal enum AcceptResult
		{
			Success,
			AlreadyHandled,
			TargetBusy
		}

		private readonly Five68DbContext _context;
		public SwapRequestFacade(Five68DbContext context)
		{
			_context = context;
		}

		internal async Task CreateRangeAsync(IEnumerable<SwapRequest> requests)
		{
			await _context.SwapRequests.AddRangeAsync(requests);
			await _context.SaveChangesAsync();
		}

		internal async Task<SwapRequest> FindByIdAsync(Guid id)
		{
			return await _context.SwapRequests.FirstOrDefaultAsync(x => x.Id == id);
		}

		internal async Task<SwapRequest> FindPendingByShiftAndTargetAsync(Guid shiftId, Guid targetEmployeeId)
		{
			return await _context.SwapRequests.FirstOrDefaultAsync(x => x.ShiftId == shiftId
				&& x.TargetEmployeeId == targetEmployeeId
				&& x.Status == SwapRequestStatus.Pending);
		}

		internal async Task<IEnumerable<SwapRequest>> GetForUserAsync(Guid userId, bool seeAll)
		{
			IQueryable<SwapRequest> query = _context.SwapRequests.OrderByDescending(x => x.CreatedAt);

			if (!seeAll)
			{
				query = query.Where(x => x.RequesterId == userId || x.TargetEmployeeId == userId);
			}
			return await query.ToListAsync();
		}

		internal async Task<AcceptResult> TryAcceptAsync(Guid swapRequestId, Guid shiftId, Guid targetEmployeeId)
		{
			await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

			if (!await TrySetStatusAsync(swapRequestId, SwapRequestStatus.Accepted))
			{
				return AcceptResult.AlreadyHandled;
			}

			Shift shift = await _context.Shifts.FirstAsync(x => x.Id == shiftId);
			bool targetBusy = await _context.Shifts.AnyAsync(x => x.Date == shift.Date && x.EmployeeId == targetEmployeeId && x.Id != shiftId);

			if (targetBusy)
			{
				return AcceptResult.TargetBusy;
			}

			await _context.Shifts
				.Where(x => x.Id == shiftId)
				.ExecuteUpdateAsync(s => s.SetProperty(x => x.EmployeeId, targetEmployeeId));

			await transaction.CommitAsync();
			return AcceptResult.Success;
		}

		internal async Task<bool> TrySetStatusAsync(Guid swapRequestId, SwapRequestStatus newStatus)
		{
			int updated = await _context.SwapRequests
				.Where(x => x.Id == swapRequestId && x.Status == SwapRequestStatus.Pending)
				.ExecuteUpdateAsync(s => s
					.SetProperty(x => x.Status, newStatus)
					.SetProperty(x => x.RespondedAt, DateTimeOffset.UtcNow));

			return updated > 0;
		}

		internal async Task DeletePendingByShiftAsync(Guid shiftId)
		{
			await _context.SwapRequests
				.Where(x => x.ShiftId == shiftId && x.Status == SwapRequestStatus.Pending)
				.ExecuteDeleteAsync();
		}
	}
}