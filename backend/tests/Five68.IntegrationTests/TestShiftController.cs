using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Five68.Models;
using Five68.Models.Authentication;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestShiftController
{
    private readonly HttpClient client_;
    private readonly Five68WebAppFactory factory_;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@five68.com";
    private const string ManagerEmail = "manager@five68.com";
    private const string EmployeeEmail = "employee@five68.com";
    private const string Password = "ValidP@ss1!";

    public TestShiftController(Five68WebAppFactory factory)
    {
        factory_ = factory;
        client_ = factory.CreateClient();
        factory_.SeedUser(AdminEmail, UserRole.Admin);
        factory_.SeedUser(ManagerEmail, UserRole.Manager);
        factory_.SeedUser(EmployeeEmail, UserRole.Employee);
    }

    private void SeedClosedDay(DateOnly date, Guid createdBy)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        db.ClosedDays.Add(new ClosedDay { Id = Guid.NewGuid(), Date = date, CreatedBy = createdBy });
        db.SaveChanges();
    }

    private Task AuthorizeAsAsync(string email) => client_.AuthorizeAsAsync(factory_, email);

    private Guid CreateEmployee(string email) => factory_.CreateEmployee(email);

    private Guid SeedShift(Guid employeeId, DateOnly date, TimeOnly start, TimeSpan duration, Guid createdBy)
        => factory_.SeedShift(employeeId, date, start, duration, createdBy);

    private Guid GetUserId(string email) => factory_.GetUserId(email);

    private bool ShiftExists(Guid id)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.Shifts.Any(x => x.Id == id);
    }

    private Shift? GetShift(DateOnly date, Guid employeeId)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.Shifts.FirstOrDefault(x => x.Date == date && x.EmployeeId == employeeId);
    }

    // --- GET /shift/{id} ---

    [Fact]
    public async Task GetById_ExistingId_Returns200WithShift()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-getbyid@five68.com");
        DateOnly date = new(2031, 1, 10);
        TimeOnly start = new(9, 0);
        TimeSpan duration = TimeSpan.FromHours(8);
        Guid shiftId = SeedShift(employeeId, date, start, duration, adminId);

        HttpResponseMessage response = await client_.GetAsync($"/shift/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.Id.Should().Be(shiftId);
        dto.EmployeeId.Should().Be(employeeId);
        dto.Date.Should().Be(date);
        dto.StartTime.Should().Be(start);
        dto.Duration.Should().Be(duration);
        dto.CreatedBy.Should().Be(adminId);
    }

    [Fact]
    public async Task GetById_JustCreatedShift_UpdatedAtIsPresent()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-getbyid-updatedat@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 1, 11), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        HttpResponseMessage response = await client_.GetAsync($"/shift/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetById_AfterUpdate_UpdatedAtIsBumped()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-getbyid-updatedat2@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 1, 12), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        ShiftDTO? original = await (await client_.GetAsync($"/shift/{shiftId}")).Content.ReadFromJsonAsync<ShiftDTO>();

        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage updateResponse = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = new TimeOnly(10, 0),
            Duration = TimeSpan.FromHours(8),
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage getResponse = await client_.GetAsync($"/shift/{shiftId}");
        ShiftDTO? updated = await getResponse.Content.ReadFromJsonAsync<ShiftDTO>();

        updated!.UpdatedAt.Should().BeAfter(original!.UpdatedAt);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync($"/shift/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync($"/shift/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /shift?startDate=&endDate= ---

    [Fact]
    public async Task GetByDateRange_ShiftsWithinRange_ReturnsOnlyThoseOrderedByDate()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid emp1 = CreateEmployee("sa-range1@five68.com");
        Guid emp2 = CreateEmployee("sa-range2@five68.com");
        Guid emp3 = CreateEmployee("sa-range3@five68.com");
        Guid empOutside = CreateEmployee("sa-range-outside@five68.com");

        DateOnly day1 = new(2031, 2, 5);
        DateOnly day2 = new(2031, 2, 6);
        DateOnly day3 = new(2031, 2, 7);
        DateOnly outsideDay = new(2031, 2, 20);
        TimeSpan duration = TimeSpan.FromHours(8);

        SeedShift(emp3, day3, new TimeOnly(9, 0), duration, adminId);
        SeedShift(emp1, day1, new TimeOnly(9, 0), duration, adminId);
        SeedShift(emp2, day2, new TimeOnly(9, 0), duration, adminId);
        SeedShift(empOutside, outsideDay, new TimeOnly(9, 0), duration, adminId);

        HttpResponseMessage response = await client_.GetAsync($"/shift?startDate={day1:yyyy-MM-dd}&endDate={day3:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ShiftDTO>? list = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        list.Should().HaveCount(3);
        list.Should().BeInAscendingOrder(x => x.Date);
        list!.Select(x => x.EmployeeId).Should().NotContain(empOutside);
    }

    [Fact]
    public async Task GetByDateRange_NoShiftsInRange_ReturnsEmptyList()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync("/shift?startDate=2099-01-01&endDate=2099-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ShiftDTO>? list = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDateRange_EndBeforeStart_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync("/shift?startDate=2031-02-10&endDate=2031-02-01");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetByDateRange_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync("/shift?startDate=2031-02-01&endDate=2031-02-10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /shift ---

    [Fact]
    public async Task Create_ManagerValidShift_Returns201AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-manager@five68.com");
        DateOnly date = new(2031, 3, 1);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.EmployeeId.Should().Be(employeeId);
        dto.Date.Should().Be(date);
        dto.Duration.Should().Be(TimeSpan.FromHours(8));
        dto.CreatedBy.Should().Be(managerId);
        ShiftExists(dto.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Create_AdminValidShift_Returns201()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid employeeId = CreateEmployee("sa-create-admin@five68.com");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = new DateOnly(2031, 3, 2),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(EmployeeEmail);
        Guid employeeId = CreateEmployee("sa-create-forbidden@five68.com");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = new DateOnly(2031, 3, 3),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = Guid.NewGuid(),
            Date = new DateOnly(2031, 3, 4),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_UnknownEmployee_Returns404()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = Guid.NewGuid(),
            Date = new DateOnly(2031, 3, 5),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ZeroDuration_Returns400()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-zeroduration@five68.com");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = new DateOnly(2031, 3, 6),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.Zero,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DurationExceeds24Hours_Returns400()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-toolong@five68.com");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = new DateOnly(2031, 3, 9),
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(25),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_OvernightDuration_Returns201AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-overnight@five68.com");
        DateOnly date = new(2031, 3, 10);
        TimeOnly start = new(22, 0);
        TimeSpan duration = TimeSpan.FromHours(4); // 22:00 -> 02:00 del giorno dopo

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = start,
            Duration = duration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.Date.Should().Be(date);
        dto.StartTime.Should().Be(start);
        dto.Duration.Should().Be(duration);
    }

    [Fact]
    public async Task Create_ClosedDay_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-closedday@five68.com");
        DateOnly date = new(2031, 3, 7);
        SeedClosedDay(date, managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_EmployeeAlreadyAssignedOnDate_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-duplicate@five68.com");
        DateOnly date = new(2031, 3, 8);
        SeedShift(employeeId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift", new ShiftCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = new TimeOnly(18, 0),
            Duration = TimeSpan.FromHours(4),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_MalformedJson_Returns400()
    {
        await AuthorizeAsAsync(ManagerEmail);
        StringContent content = new("{not-valid-json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client_.PostAsync("/shift", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- PUT /shift/{id} ---

    [Fact]
    public async Task Update_ManagerValidTimes_Returns200AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-manager@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 4, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        TimeOnly newStart = new(10, 0);
        TimeSpan newDuration = TimeSpan.FromHours(8);
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = newStart,
            Duration = newDuration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.StartTime.Should().Be(newStart);
        dto.Duration.Should().Be(newDuration);

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        Shift updated = db.Shifts.First(x => x.Id == shiftId);
        updated.StartTime.Should().Be(newStart);
        updated.Duration.Should().Be(newDuration);
    }

    [Fact]
    public async Task Update_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-update-forbidden@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 4, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        await AuthorizeAsAsync(EmployeeEmail);
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = new TimeOnly(10, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{Guid.NewGuid()}", new ShiftUpdate
        {
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{Guid.NewGuid()}", new ShiftUpdate
        {
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ZeroDuration_Returns400()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-zeroduration@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 4, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.Zero,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_DurationExceeds24Hours_Returns400()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-toolong@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 4, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(25),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_OvernightDuration_Returns200AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-overnight@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 4, 7), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        TimeOnly newStart = new(22, 0);
        TimeSpan newDuration = TimeSpan.FromHours(4); // 22:00 -> 02:00 del giorno dopo
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = newStart,
            Duration = newDuration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDTO? dto = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        dto!.StartTime.Should().Be(newStart);
        dto.Duration.Should().Be(newDuration);
    }

    [Fact]
    public async Task Update_ClosedDay_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-closedday@five68.com");
        DateOnly date = new(2031, 4, 4);
        Guid shiftId = SeedShift(employeeId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
        SeedClosedDay(date, managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shift/{shiftId}", new ShiftUpdate
        {
            StartTime = new TimeOnly(10, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /shift/{id} ---

    [Fact]
    public async Task Delete_ManagerExistingId_Returns204AndRemoves()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-delete-manager@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 5, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.DeleteAsync($"/shift/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ShiftExists(shiftId).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-delete-forbidden@five68.com");
        Guid shiftId = SeedShift(employeeId, new DateOnly(2031, 5, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        await AuthorizeAsAsync(EmployeeEmail);
        HttpResponseMessage response = await client_.DeleteAsync($"/shift/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ShiftExists(shiftId).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.DeleteAsync($"/shift/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.DeleteAsync($"/shift/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /shift/copy-week ---

    // CopyWeekAsync reads every shift in the source week regardless of employee, and the
    // "Integration" collection shares one database across all integration test classes.
    // These tests therefore use dates in 2041, a year no other integration test touches
    // (verified via grep), so no shift seeded elsewhere can leak into a source week here.

    [Fact]
    public async Task CopyWeek_ValidSingleWeek_CreatesShiftsWithDayCorrespondence()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-copyweek-single@five68.com");
        DateOnly sourceMonday = new(2041, 8, 12);
        DateOnly sourceWednesday = new(2041, 8, 14);
        TimeOnly start = new(9, 0);
        TimeSpan duration = TimeSpan.FromHours(8);
        SeedShift(employeeId, sourceWednesday, start, duration, managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = sourceMonday,
            TargetStartDate = new DateOnly(2041, 8, 19),
            TargetEndDate = new DateOnly(2041, 8, 25),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftCopyWeekResult? result = await response.Content.ReadFromJsonAsync<ShiftCopyWeekResult>(_jsonOptions);
        result!.Created.Should().Be(1);
        result.Overwritten.Should().Be(0);
        result.SkippedClosedDays.Should().Be(0);

        Shift? copied = GetShift(new DateOnly(2041, 8, 21), employeeId); // Wednesday of the target week
        copied.Should().NotBeNull();
        copied!.StartTime.Should().Be(start);
        copied.Duration.Should().Be(duration);
    }

    [Fact]
    public async Task CopyWeek_TwoNonContiguousPeriods_BothApplied()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-copyweek-noncontiguous@five68.com");
        DateOnly sourceMonday = new(2041, 9, 2);
        SeedShift(employeeId, sourceMonday, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage first = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = sourceMonday,
            TargetStartDate = new DateOnly(2041, 9, 9),
            TargetEndDate = new DateOnly(2041, 9, 15),
        });
        HttpResponseMessage second = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = sourceMonday,
            TargetStartDate = new DateOnly(2041, 9, 23),
            TargetEndDate = new DateOnly(2041, 9, 29),
        });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        GetShift(new DateOnly(2041, 9, 9), employeeId).Should().NotBeNull();
        GetShift(new DateOnly(2041, 9, 23), employeeId).Should().NotBeNull();
    }

    [Fact]
    public async Task CopyWeek_ExistingShiftOnTargetDate_Overwrites()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-copyweek-overwrite@five68.com");
        DateOnly sourceMonday = new(2041, 9, 30);
        TimeOnly newStart = new(9, 0);
        TimeSpan newDuration = TimeSpan.FromHours(8);
        SeedShift(employeeId, sourceMonday, newStart, newDuration, managerId);

        DateOnly targetMonday = new(2041, 10, 7);
        SeedShift(employeeId, targetMonday, new TimeOnly(14, 0), TimeSpan.FromHours(4), managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = sourceMonday,
            TargetStartDate = targetMonday,
            TargetEndDate = new DateOnly(2041, 10, 13),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftCopyWeekResult? result = await response.Content.ReadFromJsonAsync<ShiftCopyWeekResult>(_jsonOptions);
        result!.Created.Should().Be(0);
        result.Overwritten.Should().Be(1);

        Shift? overwritten = GetShift(targetMonday, employeeId);
        overwritten.Should().NotBeNull();
        overwritten!.StartTime.Should().Be(newStart);
        overwritten.Duration.Should().Be(newDuration);
    }

    [Fact]
    public async Task CopyWeek_ClosedDayOnTargetDate_SkipsAndCounts()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-copyweek-closedday@five68.com");
        DateOnly sourceMonday = new(2041, 10, 21);
        SeedShift(employeeId, sourceMonday, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        DateOnly targetMonday = new(2041, 10, 28);
        SeedClosedDay(targetMonday, managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = sourceMonday,
            TargetStartDate = targetMonday,
            TargetEndDate = new DateOnly(2041, 11, 3),
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftCopyWeekResult? result = await response.Content.ReadFromJsonAsync<ShiftCopyWeekResult>(_jsonOptions);
        result!.Created.Should().Be(0);
        result.SkippedClosedDays.Should().Be(1);
        GetShift(targetMonday, employeeId).Should().BeNull();
    }

    [Fact]
    public async Task CopyWeek_SourceWeekNotMonday_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 11, 5), // Tuesday
            TargetStartDate = new DateOnly(2041, 11, 11),
            TargetEndDate = new DateOnly(2041, 11, 17),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CopyWeek_TargetStartNotMonday_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 11, 11),
            TargetStartDate = new DateOnly(2041, 11, 12), // Tuesday
            TargetEndDate = new DateOnly(2041, 11, 17),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CopyWeek_TargetEndNotSunday_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 11, 11),
            TargetStartDate = new DateOnly(2041, 11, 18),
            TargetEndDate = new DateOnly(2041, 11, 19), // Tuesday
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CopyWeek_TargetEndBeforeStart_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 11, 11),
            TargetStartDate = new DateOnly(2041, 12, 2),
            TargetEndDate = new DateOnly(2041, 11, 17),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CopyWeek_EmptySourceWeek_Returns422()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 11, 25),
            TargetStartDate = new DateOnly(2041, 12, 2),
            TargetEndDate = new DateOnly(2041, 12, 8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CopyWeek_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(EmployeeEmail);
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 8, 12),
            TargetStartDate = new DateOnly(2041, 8, 19),
            TargetEndDate = new DateOnly(2041, 8, 25),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CopyWeek_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shift/copy-week", new ShiftCopyWeek
        {
            SourceWeekMonday = new DateOnly(2041, 8, 12),
            TargetStartDate = new DateOnly(2041, 8, 19),
            TargetEndDate = new DateOnly(2041, 8, 25),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
