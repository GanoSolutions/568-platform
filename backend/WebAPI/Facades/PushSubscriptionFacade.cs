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
		/// Upsert per Endpoint (non per utente): se lo stesso browser si re-iscrive dopo un cambio
		/// utente, la riga viene riassegnata invece di duplicarsi. È la difesa lato server contro
		/// il bug "notifiche all'utente sbagliato su device condiviso".
		/// </summary>
		internal async Task UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent)
		{
			PushSubscription existing = await _context.PushSubscriptions.FirstOrDefaultAsync(x => x.Endpoint == endpoint);

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
					Active = true,
				});
			}
			else
			{
				existing.UserId = userId;
				existing.P256dh = p256dh;
				existing.Auth = auth;
				existing.UserAgent = userAgent;
				existing.Active = true;
				existing.UpdatedAt = DateTimeOffset.UtcNow;
			}

			await _context.SaveChangesAsync();
		}

		/// <summary>Disattivazione richiesta dall'utente: solo su una sua subscription.</summary>
		internal async Task<bool> DeactivateForUserAsync(Guid userId, string endpoint)
		{
			int updated = await _context.PushSubscriptions
				.Where(x => x.Endpoint == endpoint && x.UserId == userId && x.Active)
				.ExecuteUpdateAsync(s => s
					.SetProperty(x => x.Active, false)
					.SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

			return updated > 0;
		}
		/// <summary>Disattivazione automatica: l'endpoint non esiste più lato push service (404/410).</summary>
		internal async Task DeactivateByEndpointAsync(string endpoint)
		{
			await _context.PushSubscriptions
				.Where(x => x.Endpoint == endpoint && x.Active)
				.ExecuteUpdateAsync(s => s
					.SetProperty(x => x.Active, false)
					.SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));
		}

		internal async Task<bool> HasActiveAsync(Guid userId)
		{
			return await _context.PushSubscriptions.AnyAsync(x => x.UserId == userId && x.Active);
		}

		internal async Task<IEnumerable<PushSubscription>> GetActiveByUserIdsAsync(IEnumerable<Guid> userIds)
		{
			return await _context.PushSubscriptions
				.AsNoTracking()
				.Where(x => x.Active && userIds.Contains(x.UserId))
				.ToListAsync();
		}
	}

}