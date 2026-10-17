using Five68.Hubs;
using Five68.Models;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using System.Net.Http.Json;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestAppHub
{
	private readonly HttpClient _client;
	private readonly Five68WebAppFactory _factory;

	private const string ManagerEmail = "hub-manager@five68.com";

	public TestAppHub(Five68WebAppFactory factory)
	{
		_factory = factory;
		_client = factory.CreateClient();
		_factory.SeedUser(ManagerEmail, UserRole.Manager);
	}

	private HubConnection BuildConnection(string accessToken)
	{
		return new HubConnectionBuilder()
			.WithUrl($"{_client.BaseAddress}hubs/app", options =>
			{
				options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
				options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
			})
			.Build();
	}

	private static async Task<bool> WaitAsync<T>(TaskCompletionSource<T> signal, TimeSpan timeout)
	{
		Task completed = await Task.WhenAny(signal.Task, Task.Delay(timeout));
		return completed == signal.Task;
	}

	// --- Negotiate: autenticazione ---

	[Fact]
	public async Task Negotiate_WithoutToken_Returns401()
	{
		HttpResponseMessage response = await _client.PostAsync("/hubs/app/negotiate?negotiateVersion=1", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Negotiate_WithValidToken_Returns200()
	{
		await _client.AuthorizeAsAsync(_factory, ManagerEmail);
		string token = _client.DefaultRequestHeaders.Authorization!.Parameter!;

		HttpResponseMessage response = await _client.PostAsync($"/hubs/app/negotiate?negotiateVersion=1&access_token={token}", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// --- Broadcast: swap request ---

	[Fact]
	public async Task CreatingSwapRequest_BroadcastsSwapRequestsChanged()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("hub-sr-req@five68.com");
		Guid targetId = _factory.CreateEmployee("hub-sr-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "hub-sr-req@five68.com");
		string token = _client.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource<SwapRequestChangedEvent> eventReceived = new();
		connection.On<SwapRequestChangedEvent>("SwapRequestsChanged", payload => eventReceived.TrySetResult(payload));
		await connection.StartAsync();

		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la creazione di una swap request deve far arrivare l'evento SwapRequestsChanged");
		(await eventReceived.Task).RequesterId.Should().Be(requesterId);
		(await eventReceived.Task).Status.Should().Be(SwapRequestStatus.Pending);
	}

	[Fact]
	public async Task AcceptingSwapRequest_BroadcastsSwapRequestsChangedWithAcceptedStatus()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("hub-sr-acc-req@five68.com");
		Guid targetId = _factory.CreateEmployee("hub-sr-acc-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 4), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "hub-sr-acc-req@five68.com");
		string requesterToken = _client.DefaultRequestHeaders.Authorization!.Parameter!;
		HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		List<SwapRequestDTO>? created = await createResponse.Content.ReadFromJsonAsync<List<SwapRequestDTO>>();
		Guid swapRequestId = created![0].Id;

		await using HubConnection connection = BuildConnection(requesterToken);
		TaskCompletionSource<SwapRequestChangedEvent> eventReceived = new();
		connection.On<SwapRequestChangedEvent>("SwapRequestsChanged", payload =>
		{
			if (payload.Status == SwapRequestStatus.Accepted)
			{
				eventReceived.TrySetResult(payload);
			}
		});
		await connection.StartAsync();

		await _client.AuthorizeAsAsync(_factory, "hub-sr-acc-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{swapRequestId}/accept", null);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("l'accettazione di una swap request deve far arrivare l'evento SwapRequestsChanged con lo stato aggiornato");
		(await eventReceived.Task).RequesterId.Should().Be(requesterId);
	}

	// --- Broadcast: turni ---

	[Fact]
	public async Task CreatingShift_BroadcastsShiftsChanged()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid employeeId = _factory.CreateEmployee("hub-shift-emp@five68.com");

		await _client.AuthorizeAsAsync(_factory, ManagerEmail);
		string token = _client.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource<ShiftChangedEvent> eventReceived = new();
		connection.On<ShiftChangedEvent>("ShiftsChanged", payload => eventReceived.TrySetResult(payload));
		await connection.StartAsync();

		DateOnly shiftDate = new(2031, 8, 2);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/Shift", new ShiftCreate
		{
			EmployeeId = employeeId,
			Date = shiftDate,
			StartTime = new TimeOnly(9, 0),
			Duration = TimeSpan.FromHours(8),
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la creazione di un turno deve far arrivare l'evento ShiftsChanged");
		(await eventReceived.Task).Date.Should().Be(shiftDate);
	}

	[Fact]
	public async Task DeletingShift_BroadcastsShiftsChanged()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid employeeId = _factory.CreateEmployee("hub-shift-del-emp@five68.com");
		DateOnly shiftDate = new(2031, 8, 3);
		Guid shiftId = _factory.SeedShift(employeeId, shiftDate, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, ManagerEmail);
		string token = _client.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource<ShiftChangedEvent> eventReceived = new();
		connection.On<ShiftChangedEvent>("ShiftsChanged", payload => eventReceived.TrySetResult(payload));
		await connection.StartAsync();

		HttpResponseMessage response = await _client.DeleteAsync($"/Shift/{shiftId}");
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la cancellazione di un turno deve far arrivare l'evento ShiftsChanged");
		(await eventReceived.Task).Date.Should().Be(shiftDate);
	}
}