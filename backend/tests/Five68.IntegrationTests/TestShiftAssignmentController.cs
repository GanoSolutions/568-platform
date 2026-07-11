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
public class TestShiftAssignmentController
{
    private readonly HttpClient client_;
    private readonly Five68WebAppFactory factory_;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@five68.com";
    private const string ManagerEmail = "manager@five68.com";
    private const string EmployeeEmail = "employee@five68.com";
    private const string Password = "ValidP@ss1!";

    public TestShiftAssignmentController(Five68WebAppFactory factory)
    {
        factory_ = factory;
        client_ = factory.CreateClient();
        SeedUser(AdminEmail, UserRole.Admin);
        SeedUser(ManagerEmail, UserRole.Manager);
        SeedUser(EmployeeEmail, UserRole.Employee);
    }

    private void SeedUser(string email, UserRole role)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        if (db.Users.Any(u => u.Email == email))
            return;

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            Role = role,
            Status = UserStatus.Active,
        });
        db.SaveChanges();
    }

    // Crea un utente Employee completo di riga t_employees, cosi' puo' essere
    // usato come EmployeeId target di uno ShiftAssignment (FK richiesta).
    private Guid CreateEmployee(string email)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        Guid id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            Role = UserRole.Employee,
            Status = UserStatus.Active,
        });
        db.Employees.Add(new Employee
        {
            UserId = id,
            Name = "Mario",
            Surname = "Rossi",
            FiscalCode = $"FC{Guid.NewGuid():N}"[..16],
            Phone = "3331234567",
        });
        db.SaveChanges();
        return id;
    }

    private Guid SeedShiftAssignment(Guid employeeId, DateOnly date, TimeOnly start, TimeSpan duration, Guid createdBy)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        Guid id = Guid.NewGuid();
        db.ShiftAssignments.Add(new ShiftAssignment
        {
            Id = id,
            Date = date,
            EmployeeId = employeeId,
            StartTime = start,
            Duration = duration,
            CreatedBy = createdBy,
        });
        db.SaveChanges();
        return id;
    }

    private void SeedClosedDay(DateOnly date, Guid createdBy)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        db.ClosedDays.Add(new ClosedDay { Id = Guid.NewGuid(), Date = date, CreatedBy = createdBy });
        db.SaveChanges();
    }

    private async Task AuthorizeAsAsync(string email)
    {
        using (IServiceScope scope = factory_.Services.CreateScope())
        {
            Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
            User user = db.Users.First(u => u.Email == email);
            user.Status = UserStatus.Active;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
            db.SaveChanges();
        }

        HttpResponseMessage response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
        {
            Email = email,
            Password = Password,
        });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Login failed for {email}: {response.StatusCode} — {body}");
        Tokens? tokens = JsonSerializer.Deserialize<Tokens>(body, _jsonOptions);
        client_.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private Guid GetUserId(string email)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.Users.First(u => u.Email == email).Id;
    }

    private bool ShiftAssignmentExists(Guid id)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.ShiftAssignments.Any(x => x.Id == id);
    }

    // --- GET /shiftassignment/{id} ---

    [Fact]
    public async Task GetById_ExistingId_Returns200WithShiftAssignment()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-getbyid@five68.com");
        DateOnly date = new(2031, 1, 10);
        TimeOnly start = new(9, 0);
        TimeSpan duration = TimeSpan.FromHours(8);
        Guid shiftId = SeedShiftAssignment(employeeId, date, start, duration, adminId);

        HttpResponseMessage response = await client_.GetAsync($"/shiftassignment/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftAssignmentDTO? dto = await response.Content.ReadFromJsonAsync<ShiftAssignmentDTO>();
        dto!.Id.Should().Be(shiftId);
        dto.EmployeeId.Should().Be(employeeId);
        dto.Date.Should().Be(date);
        dto.StartTime.Should().Be(start);
        dto.Duration.Should().Be(duration);
        dto.CreatedBy.Should().Be(adminId);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync($"/shiftassignment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync($"/shiftassignment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /shiftassignment?startDate=&endDate= ---

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

        SeedShiftAssignment(emp3, day3, new TimeOnly(9, 0), duration, adminId);
        SeedShiftAssignment(emp1, day1, new TimeOnly(9, 0), duration, adminId);
        SeedShiftAssignment(emp2, day2, new TimeOnly(9, 0), duration, adminId);
        SeedShiftAssignment(empOutside, outsideDay, new TimeOnly(9, 0), duration, adminId);

        HttpResponseMessage response = await client_.GetAsync($"/shiftassignment?startDate={day1:yyyy-MM-dd}&endDate={day3:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ShiftAssignmentDTO>? list = await response.Content.ReadFromJsonAsync<List<ShiftAssignmentDTO>>();
        list.Should().HaveCount(3);
        list.Should().BeInAscendingOrder(x => x.Date);
        list!.Select(x => x.EmployeeId).Should().NotContain(empOutside);
    }

    [Fact]
    public async Task GetByDateRange_NoShiftsInRange_ReturnsEmptyList()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync("/shiftassignment?startDate=2099-01-01&endDate=2099-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ShiftAssignmentDTO>? list = await response.Content.ReadFromJsonAsync<List<ShiftAssignmentDTO>>();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDateRange_EndBeforeStart_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync("/shiftassignment?startDate=2031-02-10&endDate=2031-02-01");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetByDateRange_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync("/shiftassignment?startDate=2031-02-01&endDate=2031-02-10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /shiftassignment ---

    [Fact]
    public async Task Create_ManagerValidShift_Returns201AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-create-manager@five68.com");
        DateOnly date = new(2031, 3, 1);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = new TimeOnly(9, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        ShiftAssignmentDTO? dto = await response.Content.ReadFromJsonAsync<ShiftAssignmentDTO>();
        dto!.EmployeeId.Should().Be(employeeId);
        dto.Date.Should().Be(date);
        dto.Duration.Should().Be(TimeSpan.FromHours(8));
        dto.CreatedBy.Should().Be(managerId);
        ShiftAssignmentExists(dto.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Create_AdminValidShift_Returns201()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid employeeId = CreateEmployee("sa-create-admin@five68.com");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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
        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
        {
            EmployeeId = employeeId,
            Date = date,
            StartTime = start,
            Duration = duration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        ShiftAssignmentDTO? dto = await response.Content.ReadFromJsonAsync<ShiftAssignmentDTO>();
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

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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
        SeedShiftAssignment(employeeId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/shiftassignment", new ShiftAssignmentCreate
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

        HttpResponseMessage response = await client_.PostAsync("/shiftassignment", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- PUT /shiftassignment/{id} ---

    [Fact]
    public async Task Update_ManagerValidTimes_Returns200AndPersists()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-update-manager@five68.com");
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 4, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        TimeOnly newStart = new(10, 0);
        TimeSpan newDuration = TimeSpan.FromHours(8);
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
        {
            StartTime = newStart,
            Duration = newDuration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftAssignmentDTO? dto = await response.Content.ReadFromJsonAsync<ShiftAssignmentDTO>();
        dto!.StartTime.Should().Be(newStart);
        dto.Duration.Should().Be(newDuration);

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        ShiftAssignment updated = db.ShiftAssignments.First(x => x.Id == shiftId);
        updated.StartTime.Should().Be(newStart);
        updated.Duration.Should().Be(newDuration);
    }

    [Fact]
    public async Task Update_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-update-forbidden@five68.com");
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 4, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        await AuthorizeAsAsync(EmployeeEmail);
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
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
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{Guid.NewGuid()}", new ShiftAssignmentUpdate
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
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{Guid.NewGuid()}", new ShiftAssignmentUpdate
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
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 4, 3), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
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
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 4, 6), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
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
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 4, 7), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        TimeOnly newStart = new(22, 0);
        TimeSpan newDuration = TimeSpan.FromHours(4); // 22:00 -> 02:00 del giorno dopo
        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
        {
            StartTime = newStart,
            Duration = newDuration,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftAssignmentDTO? dto = await response.Content.ReadFromJsonAsync<ShiftAssignmentDTO>();
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
        Guid shiftId = SeedShiftAssignment(employeeId, date, new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);
        SeedClosedDay(date, managerId);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/shiftassignment/{shiftId}", new ShiftAssignmentUpdate
        {
            StartTime = new TimeOnly(10, 0),
            Duration = TimeSpan.FromHours(8),
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /shiftassignment/{id} ---

    [Fact]
    public async Task Delete_ManagerExistingId_Returns204AndRemoves()
    {
        await AuthorizeAsAsync(ManagerEmail);
        Guid managerId = GetUserId(ManagerEmail);
        Guid employeeId = CreateEmployee("sa-delete-manager@five68.com");
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 5, 1), new TimeOnly(9, 0), TimeSpan.FromHours(8), managerId);

        HttpResponseMessage response = await client_.DeleteAsync($"/shiftassignment/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ShiftAssignmentExists(shiftId).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_EmployeeRole_Returns403()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid adminId = GetUserId(AdminEmail);
        Guid employeeId = CreateEmployee("sa-delete-forbidden@five68.com");
        Guid shiftId = SeedShiftAssignment(employeeId, new DateOnly(2031, 5, 2), new TimeOnly(9, 0), TimeSpan.FromHours(8), adminId);

        await AuthorizeAsAsync(EmployeeEmail);
        HttpResponseMessage response = await client_.DeleteAsync($"/shiftassignment/{shiftId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ShiftAssignmentExists(shiftId).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.DeleteAsync($"/shiftassignment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(ManagerEmail);
        HttpResponseMessage response = await client_.DeleteAsync($"/shiftassignment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
