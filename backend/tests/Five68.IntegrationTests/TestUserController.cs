using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Five68.Models;
using Five68.Models.Authentication;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestUserController
{
    private readonly HttpClient client_;
    private readonly Five68WebAppFactory factory_;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@five68.com";
    private const string ManagerEmail = "manager@five68.com";
    private const string EmployeeEmail = "employee@five68.com";
    // Dedicato ai test che devono invitare un account effettivamente in stato Pending —
    // EmployeeEmail resta Active perché serve per autenticarsi come Employee altrove.
    private const string PendingEmployeeEmail = "pending-employee@five68.com";
    private const string Password = "ValidP@ss1!";

    public TestUserController(Five68WebAppFactory factory)
    {
        factory_ = factory;
        client_ = factory.CreateClient();
        SeedUser(AdminEmail, Password, UserRole.Admin);
        SeedUser(ManagerEmail, Password, UserRole.Manager);
        SeedUser(EmployeeEmail, Password, UserRole.Employee);
        SeedUser(PendingEmployeeEmail, Password, UserRole.Employee, UserStatus.Pending);
    }

    private void SeedUser(string email, string password, UserRole role, UserStatus status = UserStatus.Active)
    {
        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

        if (db.Users.Any(u => u.Email == email))
            return;

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            Role = role,
            Status = status,
        });
        db.SaveChanges();
    }

    // --- GET /user ---

    [Fact]
    public async Task GetUsers_Authenticated_Returns200WithList()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync("/user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<UserDTO>? users = await response.Content.ReadFromJsonAsync<List<UserDTO>>();
        users.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsers_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync("/user");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /user/{id} ---

    [Fact]
    public async Task GetUserById_ExistingId_Returns200WithUser()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(AdminEmail);

        HttpResponseMessage response = await client_.GetAsync($"/user/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserDTO? user = await response.Content.ReadFromJsonAsync<UserDTO>();
        user!.Email.Should().Be(AdminEmail);
    }

    [Fact]
    public async Task GetUserById_UnknownId_Returns404()
    {
        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.GetAsync($"/user/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserById_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.GetAsync($"/user/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /user/{id}/invite ---

    [Fact]
    public async Task Invite_AdminInvitesEmployee_Returns200WithToken()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(PendingEmployeeEmail);

        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Dictionary<string, string>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["inviteToken"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Invite_AdminInvitesEmployee_SetsStatusToPending()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(PendingEmployeeEmail);

        await client_.PostAsync($"/user/{id}/invite", null);

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        User user = db.Users.First(u => u.Id == id);
        user.Status.Should().Be(UserStatus.Pending);
        user.InviteToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Invite_EmployeeInvites_Returns403()
    {
        await AuthorizeAsAsync(EmployeeEmail);
        Guid id = GetUserId(AdminEmail);

        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invite_UnknownUser_Returns404()
    {
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.PostAsync($"/user/{Guid.NewGuid()}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invite_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        Guid id = GetUserId(PendingEmployeeEmail);

        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invite_TargetActive_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(ManagerEmail);

        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Invite_TargetDisabled_Returns422()
    {
        (Guid id, _) = await CreateInvitedUserAsync("invite-target-disabled@five68.com", "RSSMRA80A01H501T");
        await AuthorizeAsAsync(AdminEmail);
        await client_.DeleteAsync($"/user/{id}");

        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- GET /user/{id}/invite-link ---

    [Fact]
    public async Task InviteLink_AdminGeneratesLink_Returns200WithToken()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(PendingEmployeeEmail);

        HttpResponseMessage response = await client_.GetAsync($"/user/{id}/invite-link");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Dictionary<string, string>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["inviteToken"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InviteLink_EmployeeRequests_Returns403()
    {
        await AuthorizeAsAsync(EmployeeEmail);
        Guid id = GetUserId(AdminEmail);

        HttpResponseMessage response = await client_.GetAsync($"/user/{id}/invite-link");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InviteLink_TargetActive_Returns422()
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(ManagerEmail);

        HttpResponseMessage response = await client_.GetAsync($"/user/{id}/invite-link");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /user/{id} ---

    [Fact]
    public async Task Delete_AdminDisablesEmployee_Returns204()
    {
        (Guid id, _) = await CreateInvitedUserAsync("delete-returns204@five68.com", "RSSMRA80A01H501S");

        await AuthorizeAsAsync(AdminEmail);
        HttpResponseMessage response = await client_.DeleteAsync($"/user/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_SetsStatusToDisabled()
    {
        (Guid id, _) = await CreateInvitedUserAsync("delete-setsdisabled@five68.com", "RSSMRA80A01H501R");

        await AuthorizeAsAsync(AdminEmail);
        await client_.DeleteAsync($"/user/{id}");

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        db.Users.First(u => u.Id == id).Status.Should().Be(UserStatus.Disabled);
    }

    [Fact]
    public async Task Delete_RevokesRefreshToken()
    {
        const string email = "delete-revokes-token@five68.com";
        (Guid id, _) = await CreateInvitedUserAsync(email, "RSSMRA80A01H501Q");

        using (IServiceScope scope = factory_.Services.CreateScope())
        {
            Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
            User user = db.Users.First(u => u.Id == id);
            user.Status = UserStatus.Active;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
            db.SaveChanges();
        }

        HttpResponseMessage loginResponse = await client_.PostAsJsonAsync("/auth/login", new UserLogin
        {
            Email = email,
            Password = Password,
        });
        Tokens? tokens = await loginResponse.Content.ReadFromJsonAsync<Tokens>();

        await AuthorizeAsAsync(AdminEmail);
        await client_.DeleteAsync($"/user/{id}");

        using (IServiceScope scope = factory_.Services.CreateScope())
        {
            Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
            db.RefreshTokens.Any(t => t.UserId == id).Should().BeFalse();
        }

        HttpResponseMessage refreshResponse = await client_.PostAsJsonAsync("/auth/refresh", tokens);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_EmployeeDeletes_Returns403()
    {
        await AuthorizeAsAsync(EmployeeEmail);
        Guid id = GetUserId(AdminEmail);

        HttpResponseMessage response = await client_.DeleteAsync($"/user/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownUser_Returns404()
    {
        await AuthorizeAsAsync(AdminEmail);

        HttpResponseMessage response = await client_.DeleteAsync($"/user/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        client_.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await client_.DeleteAsync($"/user/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /employee: race condition su unicità (email/codice fiscale) ---

    [Fact]
    public async Task Create_ConcurrentDuplicateFiscalCode_OneSucceedsOneReturns422()
    {
        await AuthorizeAsAsync(AdminEmail);

        EmployeeCreate first = new()
        {
            Name = "Mario",
            Surname = "Rossi",
            FiscalCode = "RSSMRA80A01H501P",
            Email = "race-1@five68.com",
            Phone = "3331234567",
        };
        EmployeeCreate second = new()
        {
            Name = "Mario",
            Surname = "Rossi",
            FiscalCode = "RSSMRA80A01H501P",
            Email = "race-2@five68.com",
            Phone = "3331234567",
        };

        Task<HttpResponseMessage> firstRequest = client_.PostAsJsonAsync("/employee", first);
        Task<HttpResponseMessage> secondRequest = client_.PostAsJsonAsync("/employee", second);
        HttpStatusCode[] statusCodes = (await Task.WhenAll(firstRequest, secondRequest))
            .Select(r => r.StatusCode)
            .ToArray();

        statusCodes.Should().Contain(HttpStatusCode.Created);
        statusCodes.Should().Contain(HttpStatusCode.UnprocessableEntity);
        statusCodes.Should().NotContain(HttpStatusCode.InternalServerError);
    }

    // --- POST /user/invite/accept ---

    [Fact]
    public async Task AcceptInvite_ValidToken_Returns200()
    {
        (_, string token) = await CreateInvitedUserAsync("accept-returns200@five68.com", "RSSMRA80A01H501Z");

        HttpResponseMessage response = await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "NewP@ss1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AcceptInvite_ValidToken_SetsStatusToActive()
    {
        (Guid id, string token) = await CreateInvitedUserAsync("accept-setsactive@five68.com", "RSSMRA80A01H501Y");

        await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "NewP@ss1!"
        });

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        db.Users.First(u => u.Id == id).Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task AcceptInvite_ValidToken_ClearsInviteToken()
    {
        (Guid id, string token) = await CreateInvitedUserAsync("accept-clearstoken@five68.com", "RSSMRA80A01H501X");

        await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "NewP@ss1!"
        });

        using IServiceScope scope = factory_.Services.CreateScope();
        Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
        User user = db.Users.First(u => u.Id == id);
        user.InviteToken.Should().BeNull();
        user.InviteTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvite_InvalidToken_Returns401()
    {
        HttpResponseMessage response = await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = "not-a-valid-token",
            Password = "NewP@ss1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvite_ExpiredToken_Returns401()
    {
        string token = await GenerateInviteForAsync(PendingEmployeeEmail);
        Guid id = GetUserId(PendingEmployeeEmail);

        using (IServiceScope scope = factory_.Services.CreateScope())
        {
            Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
            User user = db.Users.First(u => u.Id == id);
            user.InviteTokenExpiry = DateTimeOffset.UtcNow.AddDays(-1);
            db.SaveChanges();
        }

        HttpResponseMessage response = await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "NewP@ss1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvite_TokenUsedTwice_Returns401()
    {
        (_, string token) = await CreateInvitedUserAsync("accept-usedtwice@five68.com", "RSSMRA80A01H501V");

        await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "NewP@ss1!"
        });

        HttpResponseMessage replayResponse = await client_.PostAsJsonAsync("/user/invite/accept", new InviteAccept
        {
            Token = token,
            Password = "AnotherP@ss1!"
        });

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvite_MissingToken_Returns400()
    {
        HttpResponseMessage response = await client_.PostAsJsonAsync("/user/invite/accept", new
        {
            Password = "NewP@ss1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptInvite_MissingPassword_Returns400()
    {
        string token = await GenerateInviteForAsync(PendingEmployeeEmail);

        HttpResponseMessage response = await client_.PostAsJsonAsync("/user/invite/accept", new
        {
            Token = token
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> GenerateInviteForAsync(string email)
    {
        await AuthorizeAsAsync(AdminEmail);
        Guid id = GetUserId(email);
        HttpResponseMessage response = await client_.PostAsync($"/user/{id}/invite", null);
        Dictionary<string, string>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["inviteToken"];
    }

    // L'Employee nasce sempre insieme allo User via POST /employee — AcceptInvite si limita
    // a impostare la password e attivare l'account. Ogni test usa una FiscalCode dedicata
    // (constraint unique) per non collidere con gli altri nella stessa collection.
    private async Task<(Guid id, string token)> CreateInvitedUserAsync(string email, string fiscalCode)
    {
        await AuthorizeAsAsync(AdminEmail);
        await client_.PostAsJsonAsync("/employee", new EmployeeCreate
        {
            Name = "Mario",
            Surname = "Rossi",
            FiscalCode = fiscalCode,
            Email = email,
            Phone = "3331234567",
        });

        string token = await GenerateInviteForAsync(email);
        return (GetUserId(email), token);
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
}
