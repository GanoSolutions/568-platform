using Five68.Facades;
using Microsoft.Extensions.Options;
using System.Net;
using WebPush;

namespace Five68.Services
{
	public class PushDispatcherBackgroundService : BackgroundService
	{
		private readonly PushDispatchQueue _queue;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly WebPushSettings _settings;
		private readonly ILogger _logger;
		private readonly WebPushClient _client = new();

		public PushDispatcherBackgroundService(
			PushDispatchQueue queue,
			IServiceScopeFactory scopeFactory,
			IOptions<AppSettings> settings,
			ILogger<PushDispatcherBackgroundService> logger)
		{
			_queue = queue;
			_scopeFactory = scopeFactory;
			_settings = settings.Value.WebPush;
			_logger = logger;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (!_settings.Enabled)
			{
				_logger.LogWarning("Web Push disabilitato da configurazione: nessuna notifica verrà inviata");
				return;
			}

			VapidDetails vapid = new(_settings.Subject, _settings.PublicKey, _settings.PrivateKey);
			await foreach (PushJob job in _queue.ReadAllAsync(stoppingToken))
			{
				await SendAsync(job, vapid);
			}
		}

		private async Task SendAsync(PushJob job, VapidDetails vapid)
		{
			try
			{
				WebPush.PushSubscription subscription = new(job.Endpoint, job.P256dh, job.Auth);
				await _client.SendNotificationAsync(subscription, job.PayloadJson, vapid);
			}
			catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)
			{
				try
				{

					// 404/410 = subscription non più valida (PWA disinstallata, permesso revocato):
					// si disattiva, così non la si ritenta all'infinito
					using IServiceScope scope = _scopeFactory.CreateScope();
					PushSubscriptionFacade facade = scope.ServiceProvider.GetRequiredService<PushSubscriptionFacade>();
					await facade.DeactivateByEndpointAsync(job.Endpoint);
					_logger.LogInformation("Subscription {SubscriptionId} disattivata: endpoint non più valido", job.SubscriptionId);
				}
				catch (Exception dbEx)
				{
					_logger.LogError(dbEx, "Impossibile disattivare la subscription {SubscriptionId}", job.SubscriptionId);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Invio push fallito per la subscription {SubscriptionId}", job.SubscriptionId);
			}
		}
	}
}