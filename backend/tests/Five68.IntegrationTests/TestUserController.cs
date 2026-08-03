using Five68.Models;
using Five68.Models.Authentication;
using Five68.Models.DTO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestUserController
{
	private readonly HttpClient _client;
	private readonly Five68WebAppFactory _factory;

	private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

	private const string AdminEmail = "admin@five68.com";
	private const string ManagerEmail = "manager@five68.com";
	private const string EmployeeEmail = "employee@five68.com";
	private const string Password = "ValidP@ss1!";

	public TestUserController(Five68WebAppFactory factory)
	{
		_factory = factory;
		_client = factory.CreateClient();
		SeedUser(AdminEmail, Password, UserRole.Admin);
		SeedUser(ManagerEmail, Password, UserRole.Manager);
		SeedUser(EmployeeEmail, Password, UserRole.Employee);
	}

	private void SeedUser(string email, string password, UserRole role)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		if (db.Users.Any(u => u.Email == email))
		{
			return;
		}

		db.Users.Add(new User
		{
			Id = Guid.NewGuid(),
			Email = email,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
			Role = role,
			Status = UserStatus.Active,
		});
		db.SaveChanges();
	}

	// --- GET /user ---

	[Fact]
	public async Task GetUsers_Authenticated_Returns200WithList()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.GetAsync("/user");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		List<UserDTO>? users = await response.Content.ReadFromJsonAsync<List<UserDTO>>();
		users.Should().NotBeEmpty();
	}

	[Fact]
	public async Task GetUsers_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.GetAsync("/user");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// --- GET /user/{id} ---

	[Fact]
	public async Task GetUserById_ExistingId_Returns200WithUser()
	{
		await AuthorizeAsAsync(AdminEmail);
		Guid id = GetUserId(AdminEmail);

		HttpResponseMessage response = await _client.GetAsync($"/user/{id}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		UserDTO? user = await response.Content.ReadFromJsonAsync<UserDTO>();
		user!.Email.Should().Be(AdminEmail);
	}

	[Fact]
	public async Task GetUserById_UnknownId_Returns404()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.GetAsync($"/user/{Guid.NewGuid()}");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GetUserById_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.GetAsync($"/user/{Guid.NewGuid()}");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// --- POST /user/signup ---

	[Fact]
	public async Task Signup_AdminCreatesManager_Returns201()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "new.manager@five68.com",
			Password = Password,
			Role = UserRole.Manager,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Signup_AdminCreatesEmployee_Returns201()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "new.employee@five68.com",
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Signup_ManagerCreatesEmployee_Returns201()
	{
		await AuthorizeAsAsync(ManagerEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "another.employee@five68.com",
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Created);
	}

	[Fact]
	public async Task Signup_AdminCreatesAdmin_Returns403()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "another.admin@five68.com",
			Password = Password,
			Role = UserRole.Admin,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Signup_ManagerCreatesManager_Returns403()
	{
		await AuthorizeAsAsync(ManagerEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "another.manager2@five68.com",
			Password = Password,
			Role = UserRole.Manager,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Signup_EmployeeCreatesEmployee_Returns403()
	{
		await AuthorizeAsAsync(EmployeeEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "yet.another@five68.com",
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Signup_DuplicateEmail_Returns422()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = EmployeeEmail,
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
	}

	[Fact]
	public async Task Signup_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = "noauth@five68.com",
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Signup_MissingEmail_Returns400()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new
		{
			Password = Password,
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Signup_MissingPassword_Returns400()
	{
		await AuthorizeAsAsync(AdminEmail);
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/signup", new
		{
			Email = "nopassword@five68.com",
			Role = UserRole.Employee,
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	// --- POST /user/{id}/invite ---

	[Fact]
	public async Task Invite_AdminInvitesEmployee_Returns200WithToken()
	{
		await AuthorizeAsAsync(AdminEmail);
		Guid id = GetUserId(EmployeeEmail);

		HttpResponseMessage response = await _client.PostAsync($"/user/{id}/invite", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		Dictionary<string, string>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
		body!["inviteToken"].Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Invite_AdminInvitesEmployee_SetsStatusToPending()
	{
		await AuthorizeAsAsync(AdminEmail);
		Guid id = GetUserId(EmployeeEmail);

		await _client.PostAsync($"/user/{id}/invite", null);

		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		db.Users.First(u => u.Id == id).Status.Should().Be(UserStatus.Pending);
	}

	[Fact]
	public async Task Invite_EmployeeInvites_Returns403()
	{
		await AuthorizeAsAsync(EmployeeEmail);
		Guid id = GetUserId(AdminEmail);

		HttpResponseMessage response = await _client.PostAsync($"/user/{id}/invite", null);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Invite_UnknownUser_Returns404()
	{
		await AuthorizeAsAsync(AdminEmail);

		HttpResponseMessage response = await _client.PostAsync($"/user/{Guid.NewGuid()}/invite", null);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Invite_Unauthenticated_Returns401()
	{
		_client.DefaultRequestHeaders.Authorization = null;
		Guid id = GetUserId(EmployeeEmail);

		HttpResponseMessage response = await _client.PostAsync($"/user/{id}/invite", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	// --- POST /user/invite/accept ---

	[Fact]
	public async Task AcceptInvite_ValidToken_Returns200()
	{
		(_, string token) = await CreateInvitedUserAsync("accept-returns200@five68.com");

		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501Z",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task AcceptInvite_ValidToken_SetsStatusToActive()
	{
		(Guid id, string token) = await CreateInvitedUserAsync("accept-setsactive@five68.com");

		await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501Y",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		db.Users.First(u => u.Id == id).Status.Should().Be(UserStatus.Active);
	}

	[Fact]
	public async Task AcceptInvite_ValidToken_ClearsInviteToken()
	{
		(Guid id, string token) = await CreateInvitedUserAsync("accept-clearstoken@five68.com");

		await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501X",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		User user = db.Users.First(u => u.Id == id);
		user.InviteToken.Should().BeNull();
		user.InviteTokenExpiry.Should().BeNull();
	}

	[Fact]
	public async Task AcceptInvite_InvalidToken_Returns401()
	{
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = "not-a-valid-token",
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501W",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task AcceptInvite_ExpiredToken_Returns401()
	{
		string token = await GenerateInviteForAsync(EmployeeEmail);
		Guid id = GetUserId(EmployeeEmail);

		using (IServiceScope scope = _factory.Services.CreateScope())
		{
			Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
			User user = db.Users.First(u => u.Id == id);
			user.InviteTokenExpiry = DateTimeOffset.UtcNow.AddDays(-1);
			db.SaveChanges();
		}

		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501W",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task AcceptInvite_TokenUsedTwice_Returns401()
	{
		(_, string token) = await CreateInvitedUserAsync("accept-usedtwice@five68.com");

		await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501V",
			Phone = "3331234567",
			Password = "NewP@ss1!"
		});

		HttpResponseMessage replayResponse = await _client.PostAsJsonAsync("/user/invite/accept", new InviteAccept
		{
			Token = token,
			Name = "Mario",
			Surname = "Rossi",
			FiscalCode = "RSSMRA80A01H501V",
			Phone = "3331234567",
			Password = "AnotherP@ss1!"
		});

		replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task AcceptInvite_MissingToken_Returns400()
	{
		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/invite/accept", new
		{
			Password = "NewP@ss1!"
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task AcceptInvite_MissingPassword_Returns400()
	{
		string token = await GenerateInviteForAsync(EmployeeEmail);

		HttpResponseMessage response = await _client.PostAsJsonAsync("/user/invite/accept", new
		{
			Token = token
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	private async Task<string> GenerateInviteForAsync(string email)
	{
		await AuthorizeAsAsync(AdminEmail);
		Guid id = GetUserId(email);
		HttpResponseMessage response = await _client.PostAsync($"/user/{id}/invite", null);
		Dictionary<string, string>? body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
		return body!["inviteToken"];
	}

	// Ogni test che completa davvero l'accept-invite crea il proprio Employee (PK = UserId,
	// FiscalCode unique) — serve un utente nuovo e dedicato per test, non EmployeeEmail
	// condiviso, altrimenti il secondo accept-invite nella stessa collection fallisce
	// per chiave duplicata.
	private async Task<(Guid id, string token)> CreateInvitedUserAsync(string email)
	{
		await AuthorizeAsAsync(AdminEmail);
		await _client.PostAsJsonAsync("/user/signup", new UserRegister
		{
			Email = email,
			Password = Password,
			Role = UserRole.Employee,
		});

		string token = await GenerateInviteForAsync(email);
		return (GetUserId(email), token);
	}

	private async Task AuthorizeAsAsync(string email)
	{
		using (IServiceScope scope = _factory.Services.CreateScope())
		{
			Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
			User user = db.Users.First(u => u.Email == email);
			user.Status = UserStatus.Active;
			user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
			db.SaveChanges();
		}

		HttpResponseMessage response = await _client.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = email,
			Password = Password,
		});
		string body = await response.Content.ReadAsStringAsync();
		Assert.True(response.IsSuccessStatusCode, $"Login failed for {email}: {response.StatusCode} — {body}");
		Tokens? tokens = JsonSerializer.Deserialize<Tokens>(body, _jsonOptions);
		_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
	}

	private Guid GetUserId(string email)
	{
		using IServiceScope scope = _factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.Users.First(u => u.Email == email).Id;
	}
}