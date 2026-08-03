using Five68.Models;
using Five68.Models.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Five68.IntegrationTests;

/// <summary>
/// Helper condivisi tra i test di integrazione (seeding via DbContext, login), estratti come
/// metodi di estensione invece che duplicati in ogni classe di test o messi in una classe base,
/// cosi' le classi di test restano indipendenti tra loro.
/// </summary>
public static class IntegrationTestExtensions
{
	private const string DefaultPassword = "ValidP@ss1!";
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public static void SeedUser(this Five68WebAppFactory factory, string email, UserRole role, string password = DefaultPassword)
	{
		using IServiceScope scope = factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		if (db.Users.Any(u => u.Email == email))
			return;

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

	// Crea un utente Employee completo di riga t_employees, cosi' puo' essere
	// usato come EmployeeId/target di uno Shift o di una SwapRequest (FK richiesta).
	public static Guid CreateEmployee(this Five68WebAppFactory factory, string email, string password = DefaultPassword)
	{
		using IServiceScope scope = factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		Guid id = Guid.NewGuid();
		db.Users.Add(new User
		{
			Id = id,
			Email = email,
			PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
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

	public static Guid SeedShift(this Five68WebAppFactory factory, Guid employeeId, DateOnly date, TimeOnly start, TimeSpan duration, Guid createdBy)
	{
		using IServiceScope scope = factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();

		Guid id = Guid.NewGuid();
		db.Shifts.Add(new Shift
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

	public static Guid GetUserId(this Five68WebAppFactory factory, string email)
	{
		using IServiceScope scope = factory.Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		return db.Users.First(u => u.Email == email).Id;
	}

	public static async Task AuthorizeAsAsync(this HttpClient client, Five68WebAppFactory factory, string email, string password = DefaultPassword)
	{
		using (IServiceScope scope = factory.Services.CreateScope())
		{
			Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
			User user = db.Users.First(u => u.Email == email);
			user.Status = UserStatus.Active;
			user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
			db.SaveChanges();
		}

		HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", new UserLogin
		{
			Email = email,
			Password = password,
		});
		string body = await response.Content.ReadAsStringAsync();
		Assert.True(response.IsSuccessStatusCode, $"Login failed for {email}: {response.StatusCode} — {body}");
		Tokens? tokens = JsonSerializer.Deserialize<Tokens>(body, JsonOptions);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
	}
}