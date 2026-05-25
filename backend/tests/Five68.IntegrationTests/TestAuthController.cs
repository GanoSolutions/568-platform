using System.Net;
using System.Net.Http.Json;
using Five68.Models;
using Five68.Models.Authentication;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Five68.IntegrationTests;

[Collection("Integration")]
public class TestAuthController
{
	private readonly HttpClient client_;
	private readonly Five68WebAppFactory factory_;

	private const string TestEmail = "test@five68.com";
	private const string TestPassword = "ValidP@ss1!";

	public TestAuthController(Five68WebAppFactory factory)
	{
		factory_ = factory;
		client_ = factory.CreateClient();
		SeedUser(TestEmail, TestPassword);
	}

	private void SeedUser(string email, string password)
	{
		using var scope = factory_.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		if (db.Users.Any(u => u.Email == email))
			return;

		db.Users.Add(new User
		{
			Id = Guid.NewGuid(),
			Email = email,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
			FullName = "Test User",
			Role = UserRole.Admin,
			Status = UserStatus.Active,
		});
		db.SaveChanges();
	}

	[Fact]
	public async Task Login_ValidCredentials_Returns200WithTokens()
	{
		var response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = TestEmail,
			Password = TestPassword
		});

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var tokens = await response.Content.ReadFromJsonAsync<Tokens>();
		tokens!.AccessToken.Should().NotBeNullOrEmpty();
		tokens.RefreshToken.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Login_WrongPassword_Returns401()
	{
		var response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = TestEmail,
			Password = "WrongP@ss1!"
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Login_EmptyEmail_Returns400()
	{
		var response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = "",
			Password = TestPassword
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Login_UnknownEmail_Returns401()
	{
		var response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = "nobody@five68.com",
			Password = TestPassword
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Logout_ValidToken_Returns200()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		client_.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
		var response = await client_.PostAsync("/auth/logout", null);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Logout_ValidToken_DeletesRefreshToken()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		client_.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
		await client_.PostAsync("/auth/logout", null);

		using var scope = factory_.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		db.RefreshTokens.Any(t => t.Email == TestEmail).Should().BeFalse();
	}

	[Fact]
	public async Task Logout_NoToken_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = null;
		var response = await client_.PostAsync("/auth/logout", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Logout_InvalidToken_Returns401()
	{
		client_.DefaultRequestHeaders.Authorization = new("Bearer", "this.is.not.a.valid.token");
		var response = await client_.PostAsync("/auth/logout", null);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ValidTokens_Returns200WithNewTokens()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", tokens);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var newTokens = await response.Content.ReadFromJsonAsync<Tokens>();
		newTokens!.AccessToken.Should().NotBeNullOrEmpty();
		newTokens!.RefreshToken.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Refresh_ValidTokens_OldRefreshTokenIsInvalidated()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);
		await client_.PostAsJsonAsync("/auth/refresh", tokens);

		var replayResponse = await client_.PostAsJsonAsync("/auth/refresh", tokens);

		replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ValidTokens_ReturnsRotatedTokens()
	{
		var original = await LoginAsync(TestEmail, TestPassword);

		await Task.Delay(TimeSpan.FromSeconds(1));
		var response = await client_.PostAsJsonAsync("/auth/refresh", original);
		var rotated = await response.Content.ReadFromJsonAsync<Tokens>();

		rotated!.AccessToken.Should().NotBe(original.AccessToken);
		rotated.RefreshToken.Should().NotBe(original.RefreshToken);
	}

	[Fact]
	public async Task Refresh_WrongRefreshToken_Returns401()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new Tokens
		{
			AccessToken = tokens.AccessToken,
			RefreshToken = "this-is-not-the-right-refresh-token"
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_InvalidAccessToken_Returns401()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new Tokens
		{
			AccessToken = "this.is.not.a.valid.jwt",
			RefreshToken = tokens.RefreshToken
		});

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_ExpiredRefreshToken_Returns401()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		using (var scope = factory_.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
			var stored = db.RefreshTokens.First(t => t.Email == TestEmail);
			stored.ExpirationDate = DateTime.UtcNow.AddDays(-1);
			db.SaveChanges();
		}

		var response = await client_.PostAsJsonAsync("/auth/refresh", tokens);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Refresh_EmptyAccessToken_Returns401()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new Tokens
		{
			AccessToken = "",
			RefreshToken = tokens.RefreshToken
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Refresh_EmptyRefreshToken_Returns401()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new Tokens
		{
			AccessToken = tokens.AccessToken,
			RefreshToken = ""
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Refresh_MissingAccessToken_Returns400()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new
		{
			RefreshToken = tokens.RefreshToken
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Refresh_MissingRefreshToken_Returns400()
	{
		var tokens = await LoginAsync(TestEmail, TestPassword);

		var response = await client_.PostAsJsonAsync("/auth/refresh", new
		{
			AccessToken = tokens.AccessToken
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	private async Task<Tokens> LoginAsync(string email, string password)
	{
		var response = await client_.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = email,
			Password = password
		});
		return (await response.Content.ReadFromJsonAsync<Tokens>())!;
	}
}
