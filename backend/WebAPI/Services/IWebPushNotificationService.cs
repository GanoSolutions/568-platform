using Five68.Models;

namespace Five68.Services
{
	public interface IWebPushNotificationService
	{
		/// <summary>Richiesta appena creata: notifica il collega target + Admin/Manager.</summary>
		Task NotifySwapRequestCreatedAsync(SwapRequest request);
		/// <summary>Richiesta accettata/rifiutata/annullata: notifica la controparte + Admin/Manager.</summary>
		Task NotifySwapRequestRespondedAsync(SwapRequest request);
	}
	/// <summary>Payload JSON consegnato al service worker del frontend.</summary>
	public record PushPayload(string Title, string Body, string Type, Guid SwapRequestId, DateOnly ShiftDate);
	/// <summary>Un singolo invio da eseguire: subscription destinataria + messaggio già serializzato.</summary>
	public record PushJob(Guid SubscriptionId, string Endpoint, string P256dh, string Auth, string PayloadJson);


}
