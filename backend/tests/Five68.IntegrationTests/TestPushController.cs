using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Five68.Models;
using Five68.Models.DTO;
using Five68.Services;
using Five68.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestPushController
{
	private readonly HttpClient client_;
	private readonly Five68WebAppFactory factory_;

	private const string AdminEmail = "push-admin@five68.com";
	private const string ManagerEmail = "push-manager@five68.com";

	// Chiavi ben formate (il backend ne valida la forma: P256dh = punto P-256 non compresso, 65 byte
	// con prefisso 0x04; Auth = 16 byte; entrambe base64url senza padding). Nessun invio reale parte
	// durante i test (il worker è spento), quindi non serve che corrispondano a un browser vero.
	private const string P256dh = "BMCOYrAIEnIAEXDKi4eRGU9Wp_cw1f75ZSXiXzVqlv_V7V8vvWNrqbh7nnFZHyoVP5dIt73bDqq5mtSie4jMYYo";
	private const string Auth = "tBHItJI5svbpez7KI4CCXg";

	public TestPushController(Five68WebAppFactory factory)
	{
		factory_ = factory;
		client_ = factory.CreateClient();
		factory_.SeedUser(AdminEmail, UserRole.Admin);
		factory_.SeedUser(ManagerEmail, UserRole.Manager);

		// I test sul dispatch leggono direttamente PushDispatchQueue (il "seam" prima dell'invio
		// HTTP verso il push service): funziona solo finché il worker resta spento in Testing,
		// altrimenti sarebbe lui a consumare i job — e proverebbe a fare rete davvero.
		AppSettings settings = factory_.Services.GetRequiredService<IOptions<AppSettings>>().Value;
		settings.WebPush.Enabled.Should().BeFalse(
			"i test di integrazione non devono inviare push reali: AppSettings:WebPush:Enabled deve restare assente/false in appsettings.Testing.json");
	}

	private static string NewEndpoint(string name) => $"https://fcm.googleapis.com/fcm/send/{name}";

	private List<PushSubscription> GetSubscriptions(string endpoint)
	{
		using IServiceScope scope = factory_.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.PushSubscriptions.Where(x => x.Endpoint == endpoint).ToList();
	}

	private List<PushSubscription> GetSubscriptionsForUser(Guid userId)
	{
		using IServiceScope scope = factory_.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.PushSubscriptions.Where(x => x.UserId == userId).ToList();
	}

	private void SeedSubscription(Guid userId, string endpoint)
	{
		using IServiceScope scope = factory_.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		db.PushSubscriptions.Add(new PushSubscription
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Endpoint = endpoint,
			P256dh = P256dh,
			Auth = Auth,
		});
		db.SaveChanges();
	}

	// I test sui destinatari verificano l'insieme esatto degli endpoint notificati: si parte da
	// una tabella vuota per non ereditare le subscription seminate dai test precedenti (il DB è
	// condiviso da tutta la collection, che però gira in sequenza).
	private void ClearSubscriptions()
	{
		using IServiceScope scope = factory_.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		db.PushSubscriptions.RemoveRange(db.PushSubscriptions);
		db.SaveChanges();
	}

	private async Task<HttpResponseMessage> SubscribeAsync(string endpoint, string? userAgent = null, string p256dh = P256dh, string auth = Auth)
	{
		return await client_.PostAsJsonAsync("/Push/subscribe", new PushSubscriptionCreate
		{
			Endpoint = endpoint,
			P256dh = p256dh,
			Auth = auth,
			UserAgent = userAgent,
		});
	}

	private async Task<HttpResponseMessage> UnsubscribeAsync(string endpoint)
	{
		return await client_.PostAsJsonAsync("/Push/unsubscribe", new PushUnsubscribe { Endpoint = endpoint });
	}

	private async Task<(bool Active, string? Endpoint)> GetStatusAsync()
	{
		HttpResponseMessage response = await client_.GetAsync("/Push/status");
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// GetProperty (e non TryGetProperty): "endpoint" deve esserci sempre, anche quando è null
		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		return (
			body.RootElement.GetProperty("active").GetBoolean(),
			body.RootElement.GetProperty("endpoint").GetString());
	}

	// Disabilita l'utente via API come farebbe un Admin, da un client separato: client_ resta
	// autenticato come l'utente, con un access token ancora valido fino alla scadenza.
	private async Task DisableUserAsync(Guid userId)
	{
		using HttpClient admin = factory_.CreateClient();
		await admin.AuthorizeAsAsync(factory_, AdminEmail);
		(await admin.DeleteAsync($"/user/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	/// <summary>
	/// Svuota la coda singleton condivisa e restituisce i job accodati: in Testing il worker di
	/// invio non parte, quindi i job restano nel channel e sono osservabili dal test.
	/// </summary>
	private async Task<List<PushJob>> DrainQueueAsync()
	{
		PushDispatchQueue queue = factory_.Services.GetRequiredService<PushDispatchQueue>();
		List<PushJob> jobs = [];

		using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(250));
		try
		{
			await foreach (PushJob job in queue.ReadAllAsync(cts.Token))
			{
				jobs.Add(job);
			}
		}
		catch (OperationCanceledException)
		{
			// atteso: il channel non viene mai completato, si esce solo per timeout
		}

		return jobs;
	}

	// --- POST /Push/subscribe ---

	[Fact]
	public async Task Subscribe_Authenticated_Returns204AndStoresSubscription()
	{
		factory_.SeedUser("push-sub-1@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-sub-1@five68.com");
		string endpoint = NewEndpoint("sub-1");

		await client_.AuthorizeAsAsync(factory_, "push-sub-1@five68.com");
		HttpResponseMessage response = await SubscribeAsync(endpoint, "Mozilla/5.0 (Android)");

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		List<PushSubscription> stored = GetSubscriptions(endpoint);
		stored.Should().HaveCount(1);
		stored[0].UserId.Should().Be(userId);
		stored[0].P256dh.Should().Be(P256dh);
		stored[0].Auth.Should().Be(Auth);
		stored[0].UserAgent.Should().Be("Mozilla/5.0 (Android)");
	}

	[Fact]
	public async Task Subscribe_Unauthenticated_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await SubscribeAsync(NewEndpoint("sub-anon"));

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		GetSubscriptions(NewEndpoint("sub-anon")).Should().BeEmpty();
	}

	[Fact]
	public async Task Subscribe_DisabledUserWithValidToken_Returns403AndStoresNothing()
	{
		// Un utente disabilitato può avere ancora un access token valido: senza il controllo lato
		// server ricreerebbe la subscription che la disabilitazione ha appena cancellato
		factory_.SeedUser("push-sub-disabled@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-sub-disabled@five68.com");

		await client_.AuthorizeAsAsync(factory_, "push-sub-disabled@five68.com");
		await DisableUserAsync(userId);

		HttpResponseMessage response = await SubscribeAsync(NewEndpoint("sub-disabled"));

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		GetSubscriptionsForUser(userId).Should().BeEmpty();
	}

	[Fact]
	public async Task Subscribe_MissingEndpoint_Returns400()
	{
		factory_.SeedUser("push-sub-3@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-sub-3@five68.com");

		HttpResponseMessage response = await client_.PostAsJsonAsync("/Push/subscribe", new { p256dh = P256dh, auth = Auth });

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Subscribe_EndpointNotAUrl_Returns400()
	{
		factory_.SeedUser("push-sub-4@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-sub-4@five68.com");

		HttpResponseMessage response = await client_.PostAsJsonAsync("/Push/subscribe", new
		{
			endpoint = "non-e-un-url",
			p256dh = P256dh,
			auth = Auth,
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Subscribe_MissingKeys_Returns400()
	{
		factory_.SeedUser("push-sub-5@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-sub-5@five68.com");
		string endpoint = NewEndpoint("sub-5");

		HttpResponseMessage response = await client_.PostAsJsonAsync("/Push/subscribe", new { endpoint });

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		GetSubscriptions(endpoint).Should().BeEmpty();
	}

	// Chiavi malformate farebbero fallire la cifratura a ogni invio: vanno rifiutate subito.
	[Theory]
	[InlineData("CMCOYrAIEnIAEXDKi4eRGU9Wp_cw1f75ZSXiXzVqlv_V7V8vvWNrqbh7nnFZHyoVP5dIt73bDqq5mtSie4jMYYo")] // 65 byte ma primo byte 0x08 invece di 0x04
	[InlineData(Auth)] // base64url valido ma 16 byte invece di 65 (chiavi scambiate dal client)
	[InlineData("chiave-non-valida!")] // non è base64url
	public async Task Subscribe_MalformedP256dh_Returns400AndStoresNothing(string p256dh)
	{
		factory_.SeedUser("push-sub-bad-p256dh@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-sub-bad-p256dh@five68.com");
		await client_.AuthorizeAsAsync(factory_, "push-sub-bad-p256dh@five68.com");

		HttpResponseMessage response = await SubscribeAsync(NewEndpoint("sub-bad-p256dh"), p256dh: p256dh);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		GetSubscriptionsForUser(userId).Should().BeEmpty();
	}

	[Theory]
	[InlineData("tBHItJI5svbpez7KI4CC")] // 15 byte invece di 16
	[InlineData(P256dh)] // base64url valido ma 65 byte invece di 16 (chiavi scambiate dal client)
	[InlineData("auth!!")] // non è base64url
	public async Task Subscribe_MalformedAuth_Returns400AndStoresNothing(string auth)
	{
		factory_.SeedUser("push-sub-bad-auth@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-sub-bad-auth@five68.com");
		await client_.AuthorizeAsAsync(factory_, "push-sub-bad-auth@five68.com");

		HttpResponseMessage response = await SubscribeAsync(NewEndpoint("sub-bad-auth"), auth: auth);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		GetSubscriptionsForUser(userId).Should().BeEmpty();
	}

	// Difesa SSRF: il worker fa una POST verso l'endpoint salvato, quindi solo push service noti.
	// I casi limite dell'allowlist stanno negli unit test dell'attributo; qui si verifica che
	// l'attributo sia davvero applicato al DTO di subscribe e che non si salvi nulla.
	[Theory]
	[InlineData("http://localhost:8080/internal")]
	[InlineData("https://169.254.169.254/latest/meta-data/")]
	[InlineData("https://fcm.googleapis.com.evil.com/fcm/send/x")]
	[InlineData("https://evil.com/")]
	public async Task Subscribe_EndpointOutsideAllowlist_Returns400AndStoresNothing(string endpoint)
	{
		factory_.SeedUser("push-ssrf@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-ssrf@five68.com");

		HttpResponseMessage response = await SubscribeAsync(endpoint);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		GetSubscriptions(endpoint).Should().BeEmpty();
	}

	[Fact]
	public async Task Subscribe_EmptyEndpoint_ReportsASingleError()
	{
		// Il model binding di MVC esegue tutti gli attributi della proprietà senza fermarsi al primo:
		// un endpoint vuoto deve dare solo l'errore di [Required], non uno per ogni attributo
		factory_.SeedUser("push-sub-empty@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-sub-empty@five68.com");

		HttpResponseMessage response = await SubscribeAsync(string.Empty);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		JsonProperty endpointErrors = body.RootElement.GetProperty("errors").EnumerateObject()
			.Single(p => p.Name.Equals("Endpoint", StringComparison.OrdinalIgnoreCase));
		endpointErrors.Value.EnumerateArray().Select(e => e.GetString()).Should().ContainSingle();
	}

	[Fact]
	public async Task Subscribe_SameEndpointDifferentUser_MovesEndpointToNewUser()
	{
		// Regressione "device condiviso": due colleghi usano lo stesso browser/tablet. Alla
		// seconda iscrizione la riga del primo utente va eliminata, altrimenti continuerebbe a
		// ricevere le notifiche destinate al secondo.
		factory_.SeedUser("push-shared-a@five68.com", UserRole.Employee);
		factory_.SeedUser("push-shared-b@five68.com", UserRole.Employee);
		Guid userA = factory_.GetUserId("push-shared-a@five68.com");
		Guid userB = factory_.GetUserId("push-shared-b@five68.com");
		string endpoint = NewEndpoint("shared-device");

		await client_.AuthorizeAsAsync(factory_, "push-shared-a@five68.com");
		(await SubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(endpoint).Single().UserId.Should().Be(userA);

		await client_.AuthorizeAsAsync(factory_, "push-shared-b@five68.com");
		(await SubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptions(endpoint).Should().ContainSingle("lo stesso endpoint non deve mai produrre due righe")
			.Which.UserId.Should().Be(userB, "l'endpoint deve passare all'ultimo utente iscritto");
		GetSubscriptionsForUser(userA).Should().BeEmpty("il vecchio proprietario del browser non deve più ricevere notifiche");
	}

	[Fact]
	public async Task Subscribe_SameEndpointDifferentUser_CallerAlreadySubscribed_ReplacesCallerRow()
	{
		// Come sopra, ma chi si iscrive ha già una sua riga su un altro device: la sua riga
		// prende l'endpoint condiviso e quella del vecchio proprietario sparisce, senza violare
		// l'unicità né su UserId né su Endpoint.
		factory_.SeedUser("push-shared-c@five68.com", UserRole.Employee);
		factory_.SeedUser("push-shared-d@five68.com", UserRole.Employee);
		Guid userC = factory_.GetUserId("push-shared-c@five68.com");
		Guid userD = factory_.GetUserId("push-shared-d@five68.com");
		string sharedEndpoint = NewEndpoint("shared-device-2");
		string ownEndpoint = NewEndpoint("shared-device-2-own");
		SeedSubscription(userC, sharedEndpoint);
		SeedSubscription(userD, ownEndpoint);

		await client_.AuthorizeAsAsync(factory_, "push-shared-d@five68.com");
		(await SubscribeAsync(sharedEndpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptionsForUser(userC).Should().BeEmpty();
		GetSubscriptionsForUser(userD).Should().ContainSingle().Which.Endpoint.Should().Be(sharedEndpoint);
		GetSubscriptions(ownEndpoint).Should().BeEmpty();
	}

	[Fact]
	public async Task Subscribe_SameUserTwoEndpoints_ReplacesPreviousSubscription()
	{
		// Una sola subscription per utente: l'ultimo device che si iscrive sostituisce il precedente
		factory_.SeedUser("push-multi@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-multi@five68.com");
		string phone = NewEndpoint("multi-phone");
		string desktop = NewEndpoint("multi-desktop");

		await client_.AuthorizeAsAsync(factory_, "push-multi@five68.com");
		(await SubscribeAsync(phone, "Android")).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SubscribeAsync(desktop, "Chrome/Windows")).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptions(phone).Should().BeEmpty();
		PushSubscription stored = GetSubscriptionsForUser(userId).Should().ContainSingle().Which;
		stored.Endpoint.Should().Be(desktop);
		stored.UserAgent.Should().Be("Chrome/Windows");
	}

	[Fact]
	public async Task Subscribe_SameUserSameEndpointTwice_RefreshesKeysWithoutDuplicating()
	{
		factory_.SeedUser("push-refresh@five68.com", UserRole.Employee);
		string endpoint = NewEndpoint("refresh");

		await client_.AuthorizeAsAsync(factory_, "push-refresh@five68.com");
		await SubscribeAsync(endpoint, "vecchio-user-agent");
		HttpResponseMessage response = await SubscribeAsync(endpoint, "nuovo-user-agent");

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		List<PushSubscription> stored = GetSubscriptions(endpoint);
		stored.Should().HaveCount(1);
		stored[0].UserAgent.Should().Be("nuovo-user-agent");
	}

	[Fact]
	public async Task Subscribe_AfterUnsubscribe_RecreatesSingleRow()
	{
		factory_.SeedUser("push-reactivate@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-reactivate@five68.com");
		string endpoint = NewEndpoint("reactivate");

		await client_.AuthorizeAsAsync(factory_, "push-reactivate@five68.com");
		(await SubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await UnsubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		HttpResponseMessage response = await SubscribeAsync(endpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptionsForUser(userId).Should().ContainSingle().Which.Endpoint.Should().Be(endpoint);
	}

	[Fact]
	public async Task SubscribeUnsubscribe_RepeatedWithDifferentEndpoints_NeverMoreThanOneRow()
	{
		// Simula il toggle ripetuto dalle impostazioni, anche cambiando browser: le iterazioni
		// pari si disiscrivono, quelle dispari lasciano la riga che la successiva deve sostituire
		factory_.SeedUser("push-loop@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-loop@five68.com");

		await client_.AuthorizeAsAsync(factory_, "push-loop@five68.com");
		for (int i = 0; i < 6; i++)
		{
			string endpoint = NewEndpoint($"loop-{i}");

			(await SubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
			GetSubscriptionsForUser(userId).Should().ContainSingle().Which.Endpoint.Should().Be(endpoint);

			if (i % 2 == 0)
			{
				(await UnsubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
				GetSubscriptionsForUser(userId).Should().BeEmpty();
			}
		}

		GetSubscriptionsForUser(userId).Should().ContainSingle().Which.Endpoint.Should().Be(NewEndpoint("loop-5"));
	}

	// --- POST /Push/unsubscribe ---

	[Fact]
	public async Task Unsubscribe_OwnSubscription_Returns204AndDeletesRow()
	{
		factory_.SeedUser("push-unsub-1@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-unsub-1@five68.com");
		string endpoint = NewEndpoint("unsub-1");
		SeedSubscription(userId, endpoint);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-1@five68.com");
		HttpResponseMessage response = await UnsubscribeAsync(endpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(endpoint).Should().BeEmpty();
	}

	[Fact]
	public async Task Unsubscribe_CalledTwice_IsIdempotent()
	{
		factory_.SeedUser("push-unsub-2@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-unsub-2@five68.com");
		string endpoint = NewEndpoint("unsub-2");
		SeedSubscription(userId, endpoint);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-2@five68.com");
		(await UnsubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		HttpResponseMessage second = await UnsubscribeAsync(endpoint);

		second.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(endpoint).Should().BeEmpty();
	}

	[Fact]
	public async Task Unsubscribe_UnknownEndpoint_Returns204()
	{
		factory_.SeedUser("push-unsub-3@five68.com", UserRole.Employee);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-3@five68.com");
		HttpResponseMessage response = await UnsubscribeAsync(NewEndpoint("mai-registrato"));

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task Unsubscribe_OtherUsersEndpoint_LeavesItIntact()
	{
		factory_.SeedUser("push-unsub-owner@five68.com", UserRole.Employee);
		factory_.SeedUser("push-unsub-intruder@five68.com", UserRole.Employee);
		Guid ownerId = factory_.GetUserId("push-unsub-owner@five68.com");
		string ownerEndpoint = NewEndpoint("unsub-owner");
		SeedSubscription(ownerId, ownerEndpoint);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-intruder@five68.com");
		HttpResponseMessage response = await UnsubscribeAsync(ownerEndpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(ownerEndpoint).Should().ContainSingle("un utente non può cancellare la subscription di un altro")
			.Which.UserId.Should().Be(ownerId);
	}

	[Fact]
	public async Task Unsubscribe_EndpointNoLongerRegistered_LeavesCurrentSubscription()
	{
		// Il vecchio device (sostituito da un'iscrizione più recente) si disiscrive col suo
		// endpoint: non deve cancellare la subscription del device attuale dello stesso utente
		factory_.SeedUser("push-unsub-5@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-unsub-5@five68.com");
		string current = NewEndpoint("unsub-5-current");
		SeedSubscription(userId, current);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-5@five68.com");
		(await UnsubscribeAsync(NewEndpoint("unsub-5-old"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptionsForUser(userId).Should().ContainSingle().Which.Endpoint.Should().Be(current);
	}

	[Fact]
	public async Task Unsubscribe_Unauthenticated_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await UnsubscribeAsync(NewEndpoint("unsub-anon"));

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// --- GET /Push/status ---

	[Fact]
	public async Task Status_NoSubscription_ReturnsInactiveWithNullEndpoint()
	{
		factory_.SeedUser("push-status-1@five68.com", UserRole.Employee);

		await client_.AuthorizeAsAsync(factory_, "push-status-1@five68.com");

		(bool active, string? endpoint) = await GetStatusAsync();
		active.Should().BeFalse();
		endpoint.Should().BeNull();
	}

	[Fact]
	public async Task Status_AfterSubscribe_ReturnsActiveWithEndpoint()
	{
		factory_.SeedUser("push-status-2@five68.com", UserRole.Employee);
		string endpoint = NewEndpoint("status-2");

		await client_.AuthorizeAsAsync(factory_, "push-status-2@five68.com");
		(await SubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		(bool active, string? returned) = await GetStatusAsync();
		active.Should().BeTrue();
		returned.Should().Be(endpoint);
	}

	[Fact]
	public async Task Status_AfterUnsubscribe_ReturnsInactiveWithNullEndpoint()
	{
		factory_.SeedUser("push-status-3@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-status-3@five68.com");
		string endpoint = NewEndpoint("status-3");
		SeedSubscription(userId, endpoint);

		await client_.AuthorizeAsAsync(factory_, "push-status-3@five68.com");
		(await GetStatusAsync()).Active.Should().BeTrue();

		(await UnsubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		(bool active, string? returned) = await GetStatusAsync();
		active.Should().BeFalse();
		returned.Should().BeNull();
	}

	[Fact]
	public async Task Status_SubscribedFromAnotherDevice_ReturnsLatestEndpoint()
	{
		// Il client confronta l'endpoint restituito con quello del proprio browser per distinguere
		// "attive su questo dispositivo" da "attive su un altro dispositivo"
		factory_.SeedUser("push-status-4@five68.com", UserRole.Employee);
		string phone = NewEndpoint("status-4-phone");
		string desktop = NewEndpoint("status-4-desktop");

		await client_.AuthorizeAsAsync(factory_, "push-status-4@five68.com");
		(await SubscribeAsync(phone)).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SubscribeAsync(desktop)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		(bool active, string? endpoint) = await GetStatusAsync();
		active.Should().BeTrue();
		endpoint.Should().Be(desktop);
	}

	[Fact]
	public async Task Status_Unauthenticated_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await client_.GetAsync("/Push/status");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Status_ReflectsOnlyTheCallersSubscriptions()
	{
		factory_.SeedUser("push-status-5-a@five68.com", UserRole.Employee);
		factory_.SeedUser("push-status-5-b@five68.com", UserRole.Employee);
		Guid userA = factory_.GetUserId("push-status-5-a@five68.com");
		SeedSubscription(userA, NewEndpoint("status-5"));

		await client_.AuthorizeAsAsync(factory_, "push-status-5-b@five68.com");

		(bool active, string? endpoint) = await GetStatusAsync();
		active.Should().BeFalse();
		endpoint.Should().BeNull();
	}

	// --- DELETE /user/{id}: disabilitazione ---

	[Fact]
	public async Task DisableUser_WithSubscription_DeletesItsSubscriptions()
	{
		factory_.SeedUser("push-disable-1@five68.com", UserRole.Employee);
		factory_.SeedUser("push-disable-1-other@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-disable-1@five68.com");
		Guid otherId = factory_.GetUserId("push-disable-1-other@five68.com");
		string otherEndpoint = NewEndpoint("disable-1-other");
		SeedSubscription(userId, NewEndpoint("disable-1"));
		SeedSubscription(otherId, otherEndpoint);

		await DisableUserAsync(userId);

		GetSubscriptionsForUser(userId).Should().BeEmpty("un utente disabilitato non deve più ricevere notifiche");
		GetSubscriptionsForUser(otherId).Should().ContainSingle("le subscription degli altri utenti non vanno toccate")
			.Which.Endpoint.Should().Be(otherEndpoint);
	}

	// --- GET /Push/vapid-public-key ---

	private const string VapidPublicKey = "BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U";

	/// <summary>
	/// Host separato con Web Push abilitato: l'host condiviso resta con Enabled=false (vedi costruttore).
	/// Il worker di invio qui parte, ma ha una sua PushDispatchQueue e nessun test vi accoda job.
	/// </summary>
	private WebApplicationFactory<Program> CreateFactoryWithWebPush(string publicKey)
	{
		return factory_.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.PostConfigure<AppSettings>(s => s.WebPush = new WebPushSettings
			{
				Enabled = true,
				PublicKey = publicKey,
				PrivateKey = "test-private-key",
				Subject = "mailto:test@five68.com",
			})));
	}

	[Fact]
	public async Task VapidPublicKey_Configured_ReturnsOnlyThePublicKey()
	{
		factory_.SeedUser("push-vapid-1@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-vapid-1@five68.com");

		await using WebApplicationFactory<Program> pushFactory = CreateFactoryWithWebPush(VapidPublicKey);
		HttpClient client = pushFactory.CreateClient();
		client.DefaultRequestHeaders.Authorization = client_.DefaultRequestHeaders.Authorization;

		HttpResponseMessage response = await client.GetAsync("/Push/vapid-public-key");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		string json = await response.Content.ReadAsStringAsync();
		using JsonDocument body = JsonDocument.Parse(json);
		body.RootElement.GetProperty("publicKey").GetString().Should().Be(VapidPublicKey);
		body.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal(["publicKey"], "la risposta deve esporre solo la chiave pubblica");
		json.Should().NotContain("test-private-key");
	}

	[Fact]
	public async Task VapidPublicKey_WebPushDisabled_Returns404()
	{
		factory_.SeedUser("push-vapid-2@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-vapid-2@five68.com");

		HttpResponseMessage response = await client_.GetAsync("/Push/vapid-public-key");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task VapidPublicKey_EnabledButKeyMissing_Returns404()
	{
		factory_.SeedUser("push-vapid-3@five68.com", UserRole.Employee);
		await client_.AuthorizeAsAsync(factory_, "push-vapid-3@five68.com");

		await using WebApplicationFactory<Program> pushFactory = CreateFactoryWithWebPush(string.Empty);
		HttpClient client = pushFactory.CreateClient();
		client.DefaultRequestHeaders.Authorization = client_.DefaultRequestHeaders.Authorization;

		HttpResponseMessage response = await client.GetAsync("/Push/vapid-public-key");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task VapidPublicKey_Unauthenticated_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await client_.GetAsync("/Push/vapid-public-key");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// --- Ciclo di vita SwapRequest: chi finisce in coda ---

	[Fact]
	public async Task CreateSwapRequest_EnqueuesPushForTargetAndSupervisors()
	{
		ClearSubscriptions();
		Guid adminId = factory_.GetUserId(AdminEmail);
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-create-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-create-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		string targetEndpoint = NewEndpoint("create-target");
		string adminEndpoint = NewEndpoint("create-admin");
		string managerEndpoint = NewEndpoint("create-manager");
		string requesterEndpoint = NewEndpoint("create-requester");
		SeedSubscription(targetId, targetEndpoint);
		SeedSubscription(adminId, adminEndpoint);
		SeedSubscription(managerId, managerEndpoint);
		SeedSubscription(requesterId, requesterEndpoint);

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-create-req@five68.com");
		HttpResponseMessage response = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		List<PushJob> jobs = await DrainQueueAsync();
		jobs.Select(x => x.Endpoint).Should().BeEquivalentTo(
			[targetEndpoint, adminEndpoint, managerEndpoint],
			"alla creazione vanno avvisati il collega target e tutti gli Admin/Manager, non il richiedente");
		jobs.Should().OnlyContain(x => x.PayloadJson.Contains("swap_request_created"));
	}

	[Fact]
	public async Task CreateSwapRequest_PayloadIsCamelCase()
	{
		// Il service worker del frontend legge il payload con chiavi camelCase: con le chiavi
		// PascalCase di default di System.Text.Json la notifica arriverebbe vuota
		ClearSubscriptions();
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-payload-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-payload-tgt@five68.com");
		DateOnly shiftDate = new(2031, 9, 7);
		Guid shiftId = factory_.SeedShift(requesterId, shiftDate, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		string targetEndpoint = NewEndpoint("payload-target");
		SeedSubscription(targetId, targetEndpoint);

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-payload-req@five68.com");
		HttpResponseMessage response = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? requests = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>();

		PushJob job = (await DrainQueueAsync()).Should().ContainSingle(x => x.Endpoint == targetEndpoint).Which;
		using JsonDocument payload = JsonDocument.Parse(job.PayloadJson);
		payload.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
			["title", "body", "type", "swapRequestId", "shiftDate"]);
		payload.RootElement.GetProperty("type").GetString().Should().Be("swap_request_created");
		payload.RootElement.GetProperty("swapRequestId").GetGuid().Should().Be(requests![0].Id);
		payload.RootElement.GetProperty("shiftDate").GetString().Should().Be("2031-09-07");
	}

	[Fact]
	public async Task AcceptSwapRequest_EnqueuesPushForRequesterAndSupervisors()
	{
		ClearSubscriptions();
		Guid adminId = factory_.GetUserId(AdminEmail);
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-accept-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-accept-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		string requesterEndpoint = NewEndpoint("accept-requester");
		string adminEndpoint = NewEndpoint("accept-admin");
		string managerEndpoint = NewEndpoint("accept-manager");
		SeedSubscription(requesterId, requesterEndpoint);
		SeedSubscription(adminId, adminEndpoint);
		SeedSubscription(managerId, managerEndpoint);
		SeedSubscription(targetId, NewEndpoint("accept-target"));

		await client_.AuthorizeAsAsync(factory_, "push-accept-req@five68.com");
		HttpResponseMessage created = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		created.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? requests = await created.Content.ReadFromJsonAsync<List<SwapRequestDTO>>();

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-accept-tgt@five68.com");
		HttpResponseMessage response = await client_.PostAsync($"/SwapRequest/{requests![0].Id}/accept", null);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		List<PushJob> jobs = await DrainQueueAsync();
		jobs.Select(x => x.Endpoint).Should().BeEquivalentTo(
			[requesterEndpoint, adminEndpoint, managerEndpoint],
			"all'accettazione va avvisato chi aveva chiesto il cambio, più gli Admin/Manager");
		jobs.Should().OnlyContain(x => x.PayloadJson.Contains("swap_request_responded"));
	}

	[Fact]
	public async Task RejectSwapRequest_EnqueuesPushForRequesterAndSupervisors()
	{
		ClearSubscriptions();
		Guid adminId = factory_.GetUserId(AdminEmail);
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-reject-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-reject-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 4), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		string requesterEndpoint = NewEndpoint("reject-requester");
		string adminEndpoint = NewEndpoint("reject-admin");
		string managerEndpoint = NewEndpoint("reject-manager");
		SeedSubscription(requesterId, requesterEndpoint);
		SeedSubscription(adminId, adminEndpoint);
		SeedSubscription(managerId, managerEndpoint);
		SeedSubscription(targetId, NewEndpoint("reject-target"));

		await client_.AuthorizeAsAsync(factory_, "push-reject-req@five68.com");
		HttpResponseMessage created = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		created.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? requests = await created.Content.ReadFromJsonAsync<List<SwapRequestDTO>>();

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-reject-tgt@five68.com");
		HttpResponseMessage response = await client_.PostAsync($"/SwapRequest/{requests![0].Id}/reject", null);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		List<PushJob> jobs = await DrainQueueAsync();
		jobs.Select(x => x.Endpoint).Should().BeEquivalentTo(
			[requesterEndpoint, adminEndpoint, managerEndpoint],
			"al rifiuto va avvisato chi aveva chiesto il cambio, più gli Admin/Manager");
	}

	[Fact]
	public async Task CancelSwapRequest_EnqueuesPushForTargetAndSupervisors()
	{
		ClearSubscriptions();
		Guid adminId = factory_.GetUserId(AdminEmail);
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-cancel-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-cancel-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 5), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		string targetEndpoint = NewEndpoint("cancel-target");
		string adminEndpoint = NewEndpoint("cancel-admin");
		string managerEndpoint = NewEndpoint("cancel-manager");
		SeedSubscription(targetId, targetEndpoint);
		SeedSubscription(adminId, adminEndpoint);
		SeedSubscription(managerId, managerEndpoint);
		SeedSubscription(requesterId, NewEndpoint("cancel-requester"));

		await client_.AuthorizeAsAsync(factory_, "push-cancel-req@five68.com");
		HttpResponseMessage created = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		created.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? requests = await created.Content.ReadFromJsonAsync<List<SwapRequestDTO>>();

		await DrainQueueAsync();

		HttpResponseMessage response = await client_.PostAsync($"/SwapRequest/{requests![0].Id}/cancel", null);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		List<PushJob> jobs = await DrainQueueAsync();
		jobs.Select(x => x.Endpoint).Should().BeEquivalentTo(
			[targetEndpoint, adminEndpoint, managerEndpoint],
			"all'annullamento va avvisato il collega a cui era stato chiesto il cambio, più gli Admin/Manager");
	}

	[Fact]
	public async Task SwapRequestLifecycle_NoSubscriptions_EnqueuesNothing()
	{
		ClearSubscriptions();
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-none-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-none-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-none-req@five68.com");
		HttpResponseMessage response = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		(await DrainQueueAsync()).Should().BeEmpty();
	}
}
