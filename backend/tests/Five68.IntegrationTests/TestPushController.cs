using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Five68.Models;
using Five68.Models.DTO;
using Five68.Services;
using FluentAssertions;
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

	// Chiavi fittizie: nessun invio reale parte durante i test (il worker è spento), quindi
	// non devono essere crittograficamente valide, solo stabili e confrontabili.
	private const string P256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA0QTpQtUbVlUls0VJXg7A8uTs1XbjhazAkj7I99e8QcYP7DkM";
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

	private static string NewEndpoint(string name) => $"https://push.example.com/{name}";

	private List<PushSubscription> GetSubscriptions(string endpoint)
	{
		using IServiceScope scope = factory_.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.PushSubscriptions.Where(x => x.Endpoint == endpoint).ToList();
	}

	private void SeedSubscription(Guid userId, string endpoint, bool active = true)
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
			Active = active,
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

	private async Task<HttpResponseMessage> SubscribeAsync(string endpoint, string? userAgent = null)
	{
		return await client_.PostAsJsonAsync("/Push/subscribe", new PushSubscriptionCreate
		{
			Endpoint = endpoint,
			P256dh = P256dh,
			Auth = Auth,
			UserAgent = userAgent,
		});
	}

	private async Task<HttpResponseMessage> UnsubscribeAsync(string endpoint)
	{
		return await client_.PostAsJsonAsync("/Push/unsubscribe", new PushUnsubscribe { Endpoint = endpoint });
	}

	private async Task<bool> GetStatusAsync()
	{
		HttpResponseMessage response = await client_.GetAsync("/Push/status");
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		return body.RootElement.GetProperty("active").GetBoolean();
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
	public async Task Subscribe_Authenticated_Returns204AndStoresActiveSubscription()
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
		stored[0].Active.Should().BeTrue("una subscription appena registrata deve essere subito utilizzabile");
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

	[Fact]
	public async Task Subscribe_SameEndpointDifferentUser_ReassignsRowInsteadOfDuplicating()
	{
		// Regressione "device condiviso": due colleghi usano lo stesso browser/tablet. Alla
		// seconda iscrizione la riga deve cambiare proprietario, altrimenti il primo utente
		// continuerebbe a ricevere le notifiche destinate al secondo.
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

		List<PushSubscription> stored = GetSubscriptions(endpoint);
		stored.Should().HaveCount(1, "lo stesso endpoint non deve mai produrre due righe");
		stored[0].UserId.Should().Be(userB, "l'endpoint deve essere riassegnato all'ultimo utente iscritto");
		stored[0].Active.Should().BeTrue();
	}

	[Fact]
	public async Task Subscribe_SameUserTwoEndpoints_KeepsBothActive()
	{
		factory_.SeedUser("push-multi@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-multi@five68.com");
		string phone = NewEndpoint("multi-phone");
		string desktop = NewEndpoint("multi-desktop");

		await client_.AuthorizeAsAsync(factory_, "push-multi@five68.com");
		(await SubscribeAsync(phone, "Android")).StatusCode.Should().Be(HttpStatusCode.NoContent);
		(await SubscribeAsync(desktop, "Chrome/Windows")).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptions(phone).Should().ContainSingle().Which.Should().Match<PushSubscription>(x => x.UserId == userId && x.Active);
		GetSubscriptions(desktop).Should().ContainSingle().Which.Should().Match<PushSubscription>(x => x.UserId == userId && x.Active);
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
	public async Task Subscribe_AfterUnsubscribe_ReactivatesSameRow()
	{
		factory_.SeedUser("push-reactivate@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-reactivate@five68.com");
		string endpoint = NewEndpoint("reactivate");
		SeedSubscription(userId, endpoint, active: false);

		await client_.AuthorizeAsAsync(factory_, "push-reactivate@five68.com");
		HttpResponseMessage response = await SubscribeAsync(endpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		List<PushSubscription> stored = GetSubscriptions(endpoint);
		stored.Should().HaveCount(1);
		stored[0].Active.Should().BeTrue();
	}

	// --- POST /Push/unsubscribe ---

	[Fact]
	public async Task Unsubscribe_OwnSubscription_Returns204AndDeactivates()
	{
		factory_.SeedUser("push-unsub-1@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-unsub-1@five68.com");
		string endpoint = NewEndpoint("unsub-1");
		SeedSubscription(userId, endpoint);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-1@five68.com");
		HttpResponseMessage response = await UnsubscribeAsync(endpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(endpoint).Single().Active.Should().BeFalse();
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
		GetSubscriptions(endpoint).Single().Active.Should().BeFalse();
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
	public async Task Unsubscribe_OtherUsersEndpoint_LeavesItActive()
	{
		factory_.SeedUser("push-unsub-owner@five68.com", UserRole.Employee);
		factory_.SeedUser("push-unsub-intruder@five68.com", UserRole.Employee);
		Guid ownerId = factory_.GetUserId("push-unsub-owner@five68.com");
		string ownerEndpoint = NewEndpoint("unsub-owner");
		SeedSubscription(ownerId, ownerEndpoint);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-intruder@five68.com");
		HttpResponseMessage response = await UnsubscribeAsync(ownerEndpoint);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		GetSubscriptions(ownerEndpoint).Single().Active.Should().BeTrue("un utente non può disattivare la subscription di un altro");
	}

	[Fact]
	public async Task Unsubscribe_OnlyDeactivatesTheGivenEndpoint()
	{
		factory_.SeedUser("push-unsub-5@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-unsub-5@five68.com");
		string phone = NewEndpoint("unsub-5-phone");
		string desktop = NewEndpoint("unsub-5-desktop");
		SeedSubscription(userId, phone);
		SeedSubscription(userId, desktop);

		await client_.AuthorizeAsAsync(factory_, "push-unsub-5@five68.com");
		(await UnsubscribeAsync(phone)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		GetSubscriptions(phone).Single().Active.Should().BeFalse();
		GetSubscriptions(desktop).Single().Active.Should().BeTrue("l'altro dispositivo dello stesso utente resta iscritto");
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
	public async Task Status_NoSubscription_ReturnsFalse()
	{
		factory_.SeedUser("push-status-1@five68.com", UserRole.Employee);

		await client_.AuthorizeAsAsync(factory_, "push-status-1@five68.com");

		(await GetStatusAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task Status_AfterSubscribe_ReturnsTrue()
	{
		factory_.SeedUser("push-status-2@five68.com", UserRole.Employee);

		await client_.AuthorizeAsAsync(factory_, "push-status-2@five68.com");
		(await SubscribeAsync(NewEndpoint("status-2"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

		(await GetStatusAsync()).Should().BeTrue();
	}

	[Fact]
	public async Task Status_AfterUnsubscribe_ReturnsFalse()
	{
		factory_.SeedUser("push-status-3@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-status-3@five68.com");
		string endpoint = NewEndpoint("status-3");
		SeedSubscription(userId, endpoint);

		await client_.AuthorizeAsAsync(factory_, "push-status-3@five68.com");
		(await GetStatusAsync()).Should().BeTrue();

		(await UnsubscribeAsync(endpoint)).StatusCode.Should().Be(HttpStatusCode.NoContent);

		(await GetStatusAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task Status_OnlyOtherDeviceStillActive_ReturnsTrue()
	{
		factory_.SeedUser("push-status-4@five68.com", UserRole.Employee);
		Guid userId = factory_.GetUserId("push-status-4@five68.com");
		SeedSubscription(userId, NewEndpoint("status-4-off"), active: false);
		SeedSubscription(userId, NewEndpoint("status-4-on"));

		await client_.AuthorizeAsAsync(factory_, "push-status-4@five68.com");

		(await GetStatusAsync()).Should().BeTrue();
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

		(await GetStatusAsync()).Should().BeFalse();
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
	public async Task CreateSwapRequest_InactiveSubscription_IsNotEnqueued()
	{
		ClearSubscriptions();
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("push-inactive-req@five68.com");
		Guid targetId = factory_.CreateEmployee("push-inactive-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 9, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		string activeEndpoint = NewEndpoint("inactive-target-on");
		SeedSubscription(targetId, NewEndpoint("inactive-target-off"), active: false);
		SeedSubscription(targetId, activeEndpoint);

		await DrainQueueAsync();

		await client_.AuthorizeAsAsync(factory_, "push-inactive-req@five68.com");
		HttpResponseMessage response = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		List<PushJob> jobs = await DrainQueueAsync();
		jobs.Select(x => x.Endpoint).Should().BeEquivalentTo([activeEndpoint]);
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
