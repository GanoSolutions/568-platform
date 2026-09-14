using System.Net;
using System.Net.Http.Json;
using Five68.Models;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestEmployeeController
{
    private readonly HttpClient client_;
    private readonly Five68WebAppFactory factory_;

    private const string AdminEmail = "admin@five68.com";

    public TestEmployeeController(Five68WebAppFactory factory)
    {
        factory_ = factory;
        client_ = factory.CreateClient();
        factory_.SeedUser(AdminEmail, UserRole.Admin);
    }

    private Task AuthorizeAsAsync(string email) => client_.AuthorizeAsAsync(factory_, email);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // Email e codice fiscale sono unique e il DB è condiviso da tutta la collection:
    // ogni test genera valori propri per non collidere con gli altri.
    private static EmployeeCreate NewEmployeeModel(DateOnly? contractEnd, string? email = null) => new()
    {
        Name = "Mario",
        Surname = "Rossi",
        FiscalCode = $"FC{Guid.NewGuid():N}"[..16],
        Email = email ?? $"employee-{Guid.NewGuid():N}@five68.com",
        Phone = "3331234567",
        ContractEnd = contractEnd,
    };

    private Employee GetEmployee(Guid id)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.Employees.First(e => e.UserId == id);
    }

    private bool UserExists(string email)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        return db.Users.Any(u => u.Email == email);
    }

    // --- POST /employee: ContractEnd ---

    [Fact]
    public async Task Create_ContractEndInPast_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/employee", NewEmployeeModel(Today.AddDays(-1)));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ContractEndToday_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/employee", NewEmployeeModel(Today));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ContractEndInPast_DoesNotCreateUser()
    {
        await AuthorizeAsAsync(AdminEmail);
        EmployeeCreate model = NewEmployeeModel(Today.AddDays(-1));

        await client_.PostAsJsonAsync("/employee", model);

        UserExists(model.Email).Should().BeFalse();
    }

    [Fact]
    public async Task Create_ContractEndInFuture_Returns201WithContractEnd()
    {
        await AuthorizeAsAsync(AdminEmail);
        DateOnly contractEnd = Today.AddMonths(1);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/employee", NewEmployeeModel(contractEnd));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        EmployeeDTO? employee = await response.Content.ReadFromJsonAsync<EmployeeDTO>();
        employee!.ContractEnd.Should().Be(contractEnd);
    }

    [Fact]
    public async Task Create_ContractEndNull_Returns201()
    {
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/employee", NewEmployeeModel(null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        EmployeeDTO? employee = await response.Content.ReadFromJsonAsync<EmployeeDTO>();
        employee!.ContractEnd.Should().BeNull();
    }

    // --- PUT /employee/{id}: ContractEnd ---

    [Fact]
    public async Task Update_ContractEndInPast_Returns422()
    {
        string email = $"employee-{Guid.NewGuid():N}@five68.com";
        Guid id = factory_.CreateEmployee(email);
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/employee/{id}", NewEmployeeModel(Today.AddDays(-1), email));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_ContractEndToday_Returns422()
    {
        string email = $"employee-{Guid.NewGuid():N}@five68.com";
        Guid id = factory_.CreateEmployee(email);
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/employee/{id}", NewEmployeeModel(Today, email));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_ContractEndInPast_DoesNotChangeEmployee()
    {
        string email = $"employee-{Guid.NewGuid():N}@five68.com";
        Guid id = factory_.CreateEmployee(email);
        await AuthorizeAsAsync(AdminEmail);

        await client_.PutAsJsonAsync($"/employee/{id}", NewEmployeeModel(Today.AddDays(-1), email));

        GetEmployee(id).ContractEnd.Should().BeNull();
    }

    [Fact]
    public async Task Update_ContractEndInFuture_Returns200AndPersists()
    {
        string email = $"employee-{Guid.NewGuid():N}@five68.com";
        Guid id = factory_.CreateEmployee(email);
        await AuthorizeAsAsync(AdminEmail);
        DateOnly contractEnd = Today.AddMonths(1);

        HttpResponseMessage response = await client_.PutAsJsonAsync($"/employee/{id}", NewEmployeeModel(contractEnd, email));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        GetEmployee(id).ContractEnd.Should().Be(contractEnd);
    }
}
