using Five68.Models;
using Microsoft.EntityFrameworkCore;

namespace Five68.Facades
{
	public class PushSubscriptionFacade
	{
		private readonly Five68DbContext _context;

		public PushSubscriptionFacade(Five68DbContext context)
		{
			_context = context;
		}

		/// <summary>
		/// Una sola subscription per utente: l'ultimo device che si iscrive sostituisce il precedente.
		/// Se lo stesso browser era di un altro utente, la sua riga va eliminata prima, altrimenti
		/// l'insert violerebbe UNIQUE(Endpoint). Va chiamato dentro una transazione.
		/// </summary>
		internal async Task UpsertForUserAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent)
		{
			await _context.PushSubscriptions
				.Where(x => x.Endpoint == endpoint && x.UserId != userId)
				.ExecuteDeleteAsync();

			PushSubscription existing = await _context.PushSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId);

			if (existing is null)
			{
				await _context.PushSubscriptions.AddAsync(new PushSubscription
				{
					Id = Guid.NewGuid(),
					UserId = userId,
					Endpoint = endpoint,
					P256dh = p256dh,
					Auth = auth,
					UserAgent = userAgent,
				});
			}
			else
			{
				existing.Endpoint = endpoint;
				existing.P256dh = p256dh;
				existing.Auth = auth;
				existing.UserAgent = userAgent;
				existing.UpdatedAt = DateTimeOffset.UtcNow;
			}

			await _context.SaveChangesAsync();
		}

		/// <summary>Disattivazione richiesta dall'utente: solo su una sua subscription.</summary>
		internal async Task DeleteForUserAsync(Guid userId, string endpoint)
		{
			await _context.PushSubscriptions
				.Where(x => x.UserId == userId && x.Endpoint == endpoint)
				.ExecuteDeleteAsync();
		}
		/// <summary>Disattivazione automatica: l'endpoint non esiste più lato push service (404/410).</summary>
		internal async Task DeleteByEndpointAsync(string endpoint)
		{
			await _context.PushSubscriptions
				.Where(x => x.Endpoint == endpoint)
				.ExecuteDeleteAsync();
		}

		internal async Task DeleteAllForUserAsync(Guid userId)
		{
			await _context.PushSubscriptions
				.Where(x => x.UserId == userId)
				.ExecuteDeleteAsync();
		}

		internal async Task<string> GetEndpointForUserAsync(Guid userId)
		{
			return await _context.PushSubscriptions
				.Where(x => x.UserId == userId)
				.Select(x => x.Endpoint)
				.FirstOrDefaultAsync();
		}

		internal async Task<IEnumerable<PushSubscription>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
		{
			return await _context.PushSubscriptions
				.AsNoTracking()
				.Where(x => userIds.Contains(x.UserId))
				.ToListAsync();
		}
	}

}