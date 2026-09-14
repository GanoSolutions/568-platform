using Five68.Facades;
using Five68.Models.DTO;

namespace Five68.Services
{
	public class PushSubscriptionService
	{
		private readonly PushSubscriptionFacade _facade;
		private readonly ILogger _logger;

		public PushSubscriptionService(PushSubscriptionFacade facade, ILogger<PushSubscriptionService> logger)
		{
			_facade = facade;
			_logger = logger;
		}

		public async Task Subscribe(PushSubscriptionCreate model, Guid userId)
		{
			await _facade.UpsertAsync(userId, model.Endpoint, model.P256dh, model.Auth, model.UserAgent);
			_logger.LogInformation("Push subscription registrata per l'utente {UserId}", userId);
		}

		/// <summary>
		/// Idempotente: nessun errore se l'endpoint non esiste o non è dell'utente.
		/// Il frontend la chiama a ogni logout, anche quando non c'era nulla di attivo.
		/// </summary>
		public async Task Unsubscribe(string endpoint, Guid userId)
		{
			await _facade.DeactivateForUserAsync(userId, endpoint);
		}

		public async Task<bool> HasActiveSubscription(Guid userId)
		{
			return await _facade.HasActiveAsync(userId);
		}
	}
}