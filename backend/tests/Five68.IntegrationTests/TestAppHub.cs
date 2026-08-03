using System.Net;
using System.Net.Http.Json;
using Five68.Models;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestAppHub
{
	private readonly HttpClient client_;
	private readonly Five68WebAppFactory factory_;

	private const string ManagerEmail = "hub-manager@five68.com";

	public TestAppHub(Five68WebAppFactory factory)
	{
		factory_ = factory;
		client_ = factory.CreateClient();
		factory_.SeedUser(ManagerEmail, UserRole.Manager);
	}

	private HubConnection BuildConnection(string accessToken)
	{
		return new HubConnectionBuilder()
			.WithUrl($"{client_.BaseAddress}hubs/app", options =>
			{
				options.HttpMessageHandlerFactory = _ => factory_.Server.CreateHandler();
				options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
			})
			.Build();
	}

	private static async Task<bool> WaitAsync(TaskCompletionSource signal, TimeSpan timeout)
	{
		Task completed = await Task.WhenAny(signal.Task, Task.Delay(timeout));
		return completed == signal.Task;
	}

	// --- Negotiate: autenticazione ---

	[Fact]
	public async Task Negotiate_WithoutToken_Returns401()
	{
		HttpResponseMessage response = await client_.PostAsync("/hubs/app/negotiate?negotiateVersion=1", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Negotiate_WithValidToken_Returns200()
	{
		await client_.AuthorizeAsAsync(factory_, ManagerEmail);
		string token = client_.DefaultRequestHeaders.Authorization!.Parameter!;

		HttpResponseMessage response = await client_.PostAsync($"/hubs/app/negotiate?negotiateVersion=1&access_token={token}", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// --- Broadcast: swap request ---

	[Fact]
	public async Task CreatingSwapRequest_BroadcastsSwapRequestsChanged()
	{
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid requesterId = factory_.CreateEmployee("hub-sr-req@five68.com");
		Guid targetId = factory_.CreateEmployee("hub-sr-tgt@five68.com");
		Guid shiftId = factory_.SeedShift(requesterId, new DateOnly(2031, 8, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await client_.AuthorizeAsAsync(factory_, "hub-sr-req@five68.com");
		string token = client_.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource eventReceived = new();
		connection.On("SwapRequestsChanged", () => eventReceived.TrySetResult());
		await connection.StartAsync();

		HttpResponseMessage response = await client_.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la creazione di una swap request deve far arrivare l'evento SwapRequestsChanged");
	}

	// --- Broadcast: turni ---

	[Fact]
	public async Task CreatingShift_BroadcastsShiftsChanged()
	{
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid employeeId = factory_.CreateEmployee("hub-shift-emp@five68.com");

		await client_.AuthorizeAsAsync(factory_, ManagerEmail);
		string token = client_.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource eventReceived = new();
		connection.On("ShiftsChanged", () => eventReceived.TrySetResult());
		await connection.StartAsync();

		HttpResponseMessage response = await client_.PostAsJsonAsync("/Shift", new ShiftCreate
		{
			EmployeeId = employeeId,
			Date = new DateOnly(2031, 8, 2),
			StartTime = new TimeOnly(9, 0),
			Duration = TimeSpan.FromHours(8),
		});
		response.StatusCode.Should().Be(HttpStatusCode.Created);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la creazione di un turno deve far arrivare l'evento ShiftsChanged");
	}

	[Fact]
	public async Task DeletingShift_BroadcastsShiftsChanged()
	{
		Guid managerId = factory_.GetUserId(ManagerEmail);
		Guid employeeId = factory_.CreateEmployee("hub-shift-del-emp@five68.com");
		Guid shiftId = factory_.SeedShift(employeeId, new DateOnly(2031, 8, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await client_.AuthorizeAsAsync(factory_, ManagerEmail);
		string token = client_.DefaultRequestHeaders.Authorization!.Parameter!;

		await using HubConnection connection = BuildConnection(token);
		TaskCompletionSource eventReceived = new();
		connection.On("ShiftsChanged", () => eventReceived.TrySetResult());
		await connection.StartAsync();

		HttpResponseMessage response = await client_.DeleteAsync($"/Shift/{shiftId}");
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		bool received = await WaitAsync(eventReceived, TimeSpan.FromSeconds(5));
		received.Should().BeTrue("la cancellazione di un turno deve far arrivare l'evento ShiftsChanged");
	}
}