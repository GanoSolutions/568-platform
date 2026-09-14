using Five68.Facades;
using Five68.Models;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Data.SqlTypes;
using System.Text.Json;

namespace Five68.Services
{
	public class WebPushNotificationService : IWebPushNotificationService
	{
		private readonly PushSubscriptionFacade _pushSubscriptionFacade;
		private readonly UserFacade _userFacade;
		private readonly PushDispatchQueue _queue;
		private readonly ILogger _logger;

		public WebPushNotificationService(
			PushSubscriptionFacade pushSubscriptionFacade,
			UserFacade userFacade,
			PushDispatchQueue queue,
			ILogger<WebPushNotificationService> logger)
		{
			_pushSubscriptionFacade = pushSubscriptionFacade;
			_userFacade = userFacade;
			_queue = queue;
			_logger = logger;
		}


		public Task NotifySwapRequestCreatedAsync(SwapRequest request)
		{
			PushPayload payload = new(
				Title: "Nuova richiesta di cambio turno",
				Body: $"Ti è stato chiesto di coprire il turno del {request.Shift.Date:dd/MM/yyyy}",
				Type: "swap_request_created",
				SwapRequestId: request.Id,
				ShiftDate: request.Shift.Date
			);

			return EnqueueForAsync([request.TargetEmployeeId], payload);
		}

		public Task NotifySwapRequestRespondedAsync(SwapRequest request)
		{
			string esito = request.Status switch
			{
				SwapRequestStatus.Accepted => "accettata",
				SwapRequestStatus.Rejected => "rifiutata",
				SwapRequestStatus.Cancelled => "annullata",
				_ => "aggiornata"
			};

			PushPayload payload = new(
				Title: $"Richiesta di cambio turno {esito}",
				Body: $"La richiesta per il turno del {request.Shift.Date:dd/MM/yyyy} è stata {esito}",
				Type: "swap_request_responded",
				SwapRequestId: request.Id,
				ShiftDate: request.Shift.Date
			);

			Guid recipient = request.Status == SwapRequestStatus.Cancelled ? request.TargetEmployeeId : request.RequesterId;
			return EnqueueForAsync([recipient], payload);
		}

		private async Task EnqueueForAsync(IEnumerable<Guid> directRecipients, PushPayload payload)
		{
			IEnumerable<User> supervisors = await _userFacade.GetByRolesAsync(UserRole.Admin, UserRole.Manager);
			HashSet<Guid> recipients = [.. directRecipients, .. supervisors.Select(u => u.Id)];

			IEnumerable<PushSubscription> subscriptions = await _pushSubscriptionFacade.GetActiveByUserIdsAsync(recipients);

			string json = JsonSerializer.Serialize(payload);
			int queued = 0;

			foreach (PushSubscription s in subscriptions)
			{
				_queue.Enqueue(new PushJob(s.Id, s.Endpoint, s.P256dh, s.Auth, json));
				queued++;
			}

			_logger.LogInformation("Queued {Count} push notification(s) for swap request {SwapRequestId}", queued, payload.SwapRequestId);
		}
	}
}