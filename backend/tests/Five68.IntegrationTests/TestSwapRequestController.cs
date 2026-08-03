using Five68.Models;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestSwapRequestController
{
	private readonly HttpClient _client;
	private readonly Five68WebAppFactory _factory;

	private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

	private const string AdminEmail = "sr-admin@five68.com";
	private const string ManagerEmail = "sr-manager@five68.com";

	public TestSwapRequestController(Five68WebAppFactory factory)
	{
		_factory = factory;
		_client = factory.CreateClient();
		_factory.SeedUser(AdminEmail, UserRole.Admin);
		_factory.SeedUser(ManagerEmail, UserRole.Manager);
	}

	private Guid SeedSwapRequest(Guid shiftId, Guid requesterId, Guid targetId, SwapRequestStatus status = SwapRequestStatus.Pending)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		Guid id = Guid.NewGuid();
		db.SwapRequests.Add(new SwapRequest
		{
			Id = id,
			ShiftId = shiftId,
			RequesterId = requesterId,
			TargetEmployeeId = targetId,
			Status = status,
		});
		db.SaveChanges();
		return id;
	}

	private SwapRequestStatus GetSwapRequestStatus(Guid id)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.SwapRequests.First(x => x.Id == id).Status;
	}

	private bool SwapRequestExists(Guid shiftId, Guid targetId)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.SwapRequests.Any(x => x.ShiftId == shiftId && x.TargetEmployeeId == targetId);
	}

	private Guid GetShiftEmployeeId(Guid shiftId)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.Shifts.First(x => x.Id == shiftId).EmployeeId;
	}


	// --- POST /SwapRequest (Create) ---

	[Fact]
	public async Task Create_SingleFreeTarget_Returns201AndPersistsPending()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-1-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-create-1-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-1-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? created = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		created.Should().HaveCount(1);
		created![0].ShiftId.Should().Be(shiftId);
		created[0].RequesterId.Should().Be(requesterId);
		created[0].TargetEmployeeId.Should().Be(targetId);
		created[0].Status.Should().Be(SwapRequestStatus.Pending);
		GetSwapRequestStatus(created[0].Id).Should().Be(SwapRequestStatus.Pending);
	}

	[Fact]
	public async Task Create_MultipleFreeTargets_Returns201WithOneRowPerTarget()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-2-req@five68.com");
		Guid targetB = _factory.CreateEmployee("sr-create-2-b@five68.com");
		Guid targetC = _factory.CreateEmployee("sr-create-2-c@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-2-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetB, targetC],
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? created = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		created.Should().HaveCount(2);
		created!.Select(x => x.TargetEmployeeId).Should().BeEquivalentTo([targetB, targetC]);
	}

	[Fact]
	public async Task Create_DuplicateTargetInList_DedupesToSingleRow()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-3-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-create-3-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-3-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId, targetId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		List<SwapRequestDTO>? created = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		created.Should().HaveCount(1);
	}

	[Fact]
	public async Task Create_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = Guid.NewGuid(),
			TargetEmployeeIds = [Guid.NewGuid()],
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Create_UnknownShift_Returns404()
	{
		_factory.CreateEmployee("sr-create-5-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-create-5-tgt@five68.com");

		await _client.AuthorizeAsAsync(_factory, "sr-create-5-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = Guid.NewGuid(),
			TargetEmployeeIds = [targetId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Create_RequesterNotShiftOwner_Returns403()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid shiftOwnerId = _factory.CreateEmployee("sr-create-6-owner@five68.com");
		_factory.CreateEmployee("sr-create-6-other@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-create-6-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(shiftOwnerId, new DateOnly(2031, 7, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-6-other@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Create_TargetIsSelf_Returns422()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-7-req@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 7), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-7-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [requesterId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	[Fact]
	public async Task Create_TargetWithoutEmployeeRecord_Returns404()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-8-req@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 8), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-8-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [Guid.NewGuid()], // nessun Employee associato
		});

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Create_TargetAlreadyBusyThatDate_Returns422AndCreatesNoRows()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-9-req@five68.com");
		Guid busyTarget = _factory.CreateEmployee("sr-create-9-busy@five68.com");
		Guid freeTarget = _factory.CreateEmployee("sr-create-9-free@five68.com");
		DateOnly date = new(2031, 7, 9);
		Guid shiftId = _factory.SeedShift(requesterId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		_factory.SeedShift(busyTarget, date, new TimeOnly(18, 0), TimeSpan.FromHours(4), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-9-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [freeTarget, busyTarget],
		});

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
		SwapRequestExists(shiftId, freeTarget).Should().BeFalse();
		SwapRequestExists(shiftId, busyTarget).Should().BeFalse();
	}

	[Fact]
	public async Task Create_DuplicatePendingRequestAlreadyExists_Returns422()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-10-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-create-10-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 10), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-10-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [targetId],
		});

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	[Fact]
	public async Task Create_EmptyTargetList_Returns400()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-create-11-req@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 7, 11), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-create-11-req@five68.com");
		HttpResponseMessage response = await _client.PostAsJsonAsync("/SwapRequest", new SwapRequestCreate
		{
			ShiftId = shiftId,
			TargetEmployeeIds = [],
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	// --- POST /SwapRequest/{id}/accept ---

	[Fact]
	public async Task Accept_ByTarget_Returns200AndReassignsShift()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-1-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-1-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-1-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		SwapRequestDTO? dto = await response.Content.ReadFromJsonAsync<SwapRequestDTO>(_jsonOptions);
		dto!.Status.Should().Be(SwapRequestStatus.Accepted);
		GetShiftEmployeeId(shiftId).Should().Be(targetId);
	}

	[Fact]
	public async Task Accept_ByAdminOnBehalfOfTarget_Returns200()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-2-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-2-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		GetShiftEmployeeId(shiftId).Should().Be(targetId);
	}

	[Fact]
	public async Task Accept_ByUnrelatedEmployee_Returns403()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-3-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-3-tgt@five68.com");
		_factory.CreateEmployee("sr-accept-3-other@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-3-other@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		GetShiftEmployeeId(shiftId).Should().Be(requesterId);
	}

	[Fact]
	public async Task Accept_ByManagerNotAdmin_Returns403()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-4-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-4-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 4), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, ManagerEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Accept_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Accept_UnknownId_Returns404()
	{
		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Accept_AlreadyHandled_Returns422()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-7-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-7-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 7), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId, SwapRequestStatus.Accepted);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-7-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	[Fact]
	public async Task Accept_OtherPendingRequestsOnSameShift_AreCancelled()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-8-req@five68.com");
		Guid targetB = _factory.CreateEmployee("sr-accept-8-b@five68.com");
		Guid targetC = _factory.CreateEmployee("sr-accept-8-c@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 8, 8), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestForB = SeedSwapRequest(shiftId, requesterId, targetB);
		Guid requestForC = SeedSwapRequest(shiftId, requesterId, targetC);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-8-b@five68.com");
		HttpResponseMessage acceptResponse = await _client.PostAsync($"/SwapRequest/{requestForB}/accept", null);
		acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		GetSwapRequestStatus(requestForC).Should().Be(SwapRequestStatus.Cancelled);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-8-c@five68.com");
		HttpResponseMessage secondAccept = await _client.PostAsync($"/SwapRequest/{requestForC}/accept", null);
		secondAccept.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	[Fact]
	public async Task Accept_TargetBecameBusyInTheMeantime_Returns422AndDoesNotReassignShift()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-accept-9-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-accept-9-tgt@five68.com");
		DateOnly date = new(2031, 8, 9);
		Guid shiftId = _factory.SeedShift(requesterId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		// Nel frattempo il target riceve un altro turno nella stessa data
		_factory.SeedShift(targetId, date, new TimeOnly(18, 0), TimeSpan.FromHours(4), managerId);

		await _client.AuthorizeAsAsync(_factory, "sr-accept-9-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/accept", null);

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
		GetShiftEmployeeId(shiftId).Should().Be(requesterId);
		GetSwapRequestStatus(requestId).Should().Be(SwapRequestStatus.Pending);
	}

	// --- POST /SwapRequest/{id}/reject ---

	[Fact]
	public async Task Reject_ByTarget_Returns200AndDoesNotReassignShift()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-reject-1-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-reject-1-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 9, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-reject-1-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		SwapRequestDTO? dto = await response.Content.ReadFromJsonAsync<SwapRequestDTO>(_jsonOptions);
		dto!.Status.Should().Be(SwapRequestStatus.Rejected);
		GetShiftEmployeeId(shiftId).Should().Be(requesterId);
	}

	[Fact]
	public async Task Reject_ByAdminOnBehalfOfTarget_Returns200()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-reject-2-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-reject-2-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 9, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Reject_ByUnrelatedEmployee_Returns403()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-reject-3-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-reject-3-tgt@five68.com");
		_factory.CreateEmployee("sr-reject-3-other@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 9, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-reject-3-other@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Reject_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Reject_UnknownId_Returns404()
	{
		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Reject_AlreadyHandled_Returns422()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-reject-6-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-reject-6-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 9, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId, SwapRequestStatus.Rejected);

		await _client.AuthorizeAsAsync(_factory, "sr-reject-6-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/reject", null);

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	// --- POST /SwapRequest/{id}/cancel ---

	[Fact]
	public async Task Cancel_ByRequester_Returns200()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-cancel-1-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-cancel-1-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 10, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-cancel-1-req@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		GetSwapRequestStatus(requestId).Should().Be(SwapRequestStatus.Cancelled);
	}

	[Fact]
	public async Task Cancel_ByAdminOnBehalfOfRequester_Returns200()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-cancel-2-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-cancel-2-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 10, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Cancel_ByTargetNotRequester_Returns403()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-cancel-3-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-cancel-3-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 10, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-cancel-3-tgt@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Cancel_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Cancel_UnknownId_Returns404()
	{
		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{Guid.NewGuid()}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Cancel_AlreadyHandled_Returns422()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-cancel-6-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-cancel-6-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 10, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId, SwapRequestStatus.Cancelled);

		await _client.AuthorizeAsAsync(_factory, "sr-cancel-6-req@five68.com");
		HttpResponseMessage response = await _client.PostAsync($"/SwapRequest/{requestId}/cancel", null);

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	// --- GET /SwapRequest ---

	[Fact]
	public async Task GetForUser_AsRequester_SeesOwnRequest()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-get-1-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-get-1-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 11, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-get-1-req@five68.com");
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<SwapRequestDTO>? list = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		list!.Select(x => x.Id).Should().Contain(requestId);
	}

	[Fact]
	public async Task GetForUser_AsTarget_SeesRequest()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-get-2-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-get-2-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 11, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-get-2-tgt@five68.com");
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<SwapRequestDTO>? list = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		list!.Select(x => x.Id).Should().Contain(requestId);
	}

	[Fact]
	public async Task GetForUser_UnrelatedEmployee_DoesNotSeeOthersRequest()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-get-3-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-get-3-tgt@five68.com");
		Guid otherId = _factory.CreateEmployee("sr-get-3-other@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 11, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, "sr-get-3-other@five68.com");
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<SwapRequestDTO>? list = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		list!.Select(x => x.Id).Should().NotContain(requestId);
	}

	[Fact]
	public async Task GetForUser_Manager_SeesUnrelatedRequest()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-get-4-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-get-4-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 11, 4), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, ManagerEmail);
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<SwapRequestDTO>? list = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		list!.Select(x => x.Id).Should().Contain(requestId);
	}

	[Fact]
	public async Task GetForUser_Admin_SeesUnrelatedRequest()
	{
		Guid managerId = _factory.GetUserId(ManagerEmail);
		Guid requesterId = _factory.CreateEmployee("sr-get-5-req@five68.com");
		Guid targetId = _factory.CreateEmployee("sr-get-5-tgt@five68.com");
		Guid shiftId = _factory.SeedShift(requesterId, new DateOnly(2031, 11, 5), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
		Guid requestId = SeedSwapRequest(shiftId, requesterId, targetId);

		await _client.AuthorizeAsAsync(_factory, AdminEmail);
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<SwapRequestDTO>? list = await response.Content.ReadFromJsonAsync<List<SwapRequestDTO>>(_jsonOptions);
		list!.Select(x => x.Id).Should().Contain(requestId);
	}

	[Fact]
	public async Task GetForUser_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.GetAsync("/SwapRequest");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}