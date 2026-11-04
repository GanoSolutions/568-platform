using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;

namespace Five68.Services
{
	public class PushSubscriptionService
	{
		private readonly PushSubscriptionFacade _facade;
		private readonly UserFacade _userFacade;
		private readonly TransactionFacade _transactionFacade;
		private readonly ILogger _logger;

		public PushSubscriptionService(
			PushSubscriptionFacade facade,
			UserFacade userFacade,
			TransactionFacade transactionFacade,
			ILogger<PushSubscriptionService> logger)
		{
			_facade = facade;
			_userFacade = userFacade;
			_transactionFacade = transactionFacade;
			_logger = logger;
		}

		public async Task Subscribe(PushSubscriptionCreate model, Guid userId)
		{
			// Un utente appena disabilitato può avere ancora un access token valido (max 5 min):
			// senza questo controllo ricreerebbe la riga che UserService.Delete ha appena cancellat
			User user = await _userFacade.FindByIdAsync(userId);
			if (user is null || user.Status == UserStatus.Disabled)
			{
				throw new ForbiddenException("Account non attivo");
			}

			await _transactionFacade.ExecuteAsync(async () =>
				await _facade.UpsertForUserAsync(userId, model.Endpoint, model.P256dh, model.Auth, model.UserAgent));
			_logger.LogInformation("Push subscription registrata per l'utente {UserId}", userId);
		}

		/// <summary>
		/// Idempotente: nessun errore se l'endpoint non esiste o non è dell'utente.
		/// Il frontend la chiama a ogni logout, anche quando non c'era nulla di attivo.
		/// </summary>
		public async Task Unsubscribe(string endpoint, Guid userId)
		{
			await _facade.DeleteForUserAsync(userId, endpoint);
		}

		public async Task<string> GetSubscribedEndpoint(Guid userId)
		{
			return await _facade.GetEndpointForUserAsync(userId);
		}
	}
}