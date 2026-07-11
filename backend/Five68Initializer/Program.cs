using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Five68;
using Five68.Models;
using System.Net;

namespace Five68.Initializer
{
	internal class Program
	{
		static void Main(string[] args)
		{
			AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
			Console.WriteLine("Hello, World!");
			Console.WriteLine("USE THIS PROJECT IN TESTING ENVIRONMENT ONLY");

			// 1. Setup Configuration
			IConfigurationRoot configuration = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.Development.json", optional: false)
				.Build();

			// 2. Setup DI & DbContext
			ServiceCollection services = new ServiceCollection();

			services.AddDbContext<Five68DbContext>(options =>
			{
				options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
			});

			ServiceProvider serviceProvider = services.BuildServiceProvider();

			string adminPassword = configuration["AppSettings:Seed:AdminPassword"] ?? throw new InvalidOperationException("Seed:AdminPassword not configured in the application settings");
			string workFactor = configuration["AppSettings:Crypto:WorkFactor"] ?? throw new InvalidOperationException("Crypto:WorkFactor not configured in the application settings");

			// 3. Run Seeding
			using (IServiceScope scope = serviceProvider.CreateScope())
			{
				Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
				db.Database.EnsureDeleted();
				db.Database.EnsureCreated();
				Console.WriteLine("Database created. Seeding data...");

				SeedData(db, adminPassword, int.Parse(workFactor));
			}

			Console.WriteLine("Seeding complete.");
		}

		private static void SeedData(Five68DbContext db, string adminPassword, int workFactor)
		{
			if (db.Users.Any())
			{
				Console.WriteLine("Database already contains data. Skipping seed.");
				return;
			}

			(Guid adminId, Guid managerId, Guid luigiId, Guid annaId, Guid marcoId, Guid giuliaId) = (
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
			);

			// Settimana corrente (oggi = mercoledì 2026-07-01)
			DateTime today = DateTime.UtcNow.Date;
			DateTime mon = today.AddDays(-2); // lun 29/06 — chiusura
			DateTime tue = today.AddDays(-1); // mar 30/06 — 2 dipendenti (1 parziale)
			DateTime wed = today;             // mer 01/07 — oggi, 3 dipendenti
			DateTime thu = today.AddDays(1);  // gio 02/07 — cambio turno pendente
			DateTime fri = today.AddDays(2);  // ven 03/07 — 3 dipendenti (1 parziale)
			DateTime sat = today.AddDays(3);  // sab 04/07 — anna (completo) + marco (notturno, a cavallo della mezzanotte)
			DateTime sun = today.AddDays(4);  // dom 05/07 — chiusura

			// Orari standard: feriale 18:00-00:00 (6h), turno ridotto 19:00-22:00 (3h),
			// turno notturno del weekend 22:00-02:00 del giorno dopo (4h, dimostra un turno
			// che attraversa la mezzanotte)
			TimeOnly fullStart = new TimeOnly(18, 0);
			TimeSpan fullDuration = TimeSpan.FromHours(6);
			TimeOnly partialStart = new TimeOnly(19, 0);
			TimeSpan partialDuration = TimeSpan.FromHours(3);
			TimeOnly nightStart = new TimeOnly(22, 0);
			TimeSpan nightDuration = TimeSpan.FromHours(4);

			(Guid luigiThuAssignmentId, Guid annaTueAssignmentId, Guid marcoFriAssignmentId) = (
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
			);

			// --- 1. USERS & EMPLOYEES ---
			// Varietà di stati: Active (ha fatto login), Pending (invito non inviato), Disabled
			db.Users.AddRange([
				new User {
					Id = adminId,
					Email = "admin@five68.com",
					Role = UserRole.Admin,
					Status = UserStatus.Active,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
				},
				new User {
					Id = managerId,
					Email = "manager@five68.com",
					Role = UserRole.Manager,
					Status = UserStatus.Active,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@1234!"),
					Employee = new Employee { UserId = managerId, Name = "Luca", Surname = "Ferretti", FiscalCode = "FRTLCU88M10F205X", Phone = "3331234567", Color = "#f43f5e" }
				},
				new User {
					Id = luigiId,
					Email = "luigi.rossi@email.com",
					Role = UserRole.Employee,
					Status = UserStatus.Active,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@1234!"),
					Employee = new Employee { UserId = luigiId, Name = "Luigi", Surname = "Rossi", FiscalCode = "RSSLGU90B02H501Z", Phone = "3342345678", ContractEnd = new DateOnly(2026, 12, 31), Color = "#10b981" }
				},
				new User {
					Id = annaId,
					Email = "anna.neri@email.com",
					Role = UserRole.Employee,
					Status = UserStatus.Active,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@1234!"),
					Employee = new Employee { UserId = annaId, Name = "Anna", Surname = "Neri", FiscalCode = "NRANNA95M52H501Y", Phone = "3364567890", ContractEnd = new DateOnly(2027, 5, 16), Color = "#f59e0b" }
				},
				new User {
					Id = marcoId,
					Email = "marco.bianchi@email.com",
					Role = UserRole.Employee,
					Status = UserStatus.Pending, // creato, invito non ancora inviato
					Employee = new Employee { UserId = marcoId, Name = "Marco", Surname = "Bianchi", FiscalCode = "BNCMRC95P15G224K", Phone = "3389876543", Color = "#8b5cf6" }
				},
				new User {
					Id = giuliaId,
					Email = "giulia.conti@email.com",
					Role = UserRole.Employee,
					Status = UserStatus.Disabled, // disabilitata
					Employee = new Employee { UserId = giuliaId, Name = "Giulia", Surname = "Conti", FiscalCode = "CNTGLI92A41H501B", Phone = "3471122334", ContractEnd = new DateOnly(2025, 12, 31), Color = "#06b6d4" }
				},
			]);

			// --- 2. CLOSED DAYS ---
			// Il locale chiude sempre di domenica e lunedì (chiusura settimanale ricorrente).
			// Nel seed la materializziamo solo per una finestra di qualche mese intorno a oggi
			// (dato di sviluppo, non serve coprire l'anno intero) — in produzione questa
			// generazione andrà rifatta periodicamente da un job dedicato (fuori scope qui),
			// altrimenti oltre la finestra generata le domeniche/lunedì risulterebbero aperte.
			// Festività italiane classiche e ferie estive restano invece sull'anno intero.
			int seedYear = today.Year;
			DateOnly weeklyClosuresStart = DateOnly.FromDateTime(today.AddMonths(-3));
			DateOnly weeklyClosuresEnd = DateOnly.FromDateTime(today.AddMonths(4));

			List<DateOnly> weeklyClosures = [];
			for (DateOnly d = weeklyClosuresStart; d <= weeklyClosuresEnd; d = d.AddDays(1))
			{
				if (d.DayOfWeek is DayOfWeek.Sunday or DayOfWeek.Monday)
				{
					weeklyClosures.Add(d);
				}
			}

			DateOnly easterMonday = EasterSunday(seedYear).AddDays(1);

			List<DateOnly> italianHolidays = [
				new DateOnly(seedYear, 1, 1),   // Capodanno
				new DateOnly(seedYear, 1, 6),   // Epifania
				easterMonday,                    // Pasquetta
				new DateOnly(seedYear, 4, 25),  // Festa della Liberazione
				new DateOnly(seedYear, 5, 1),   // Festa dei Lavoratori
				new DateOnly(seedYear, 6, 2),   // Festa della Repubblica
				new DateOnly(seedYear, 8, 15),  // Ferragosto
				new DateOnly(seedYear, 11, 1),  // Ognissanti
				new DateOnly(seedYear, 12, 8),  // Immacolata Concezione
				new DateOnly(seedYear, 12, 24), // Vigilia di Natale
				new DateOnly(seedYear, 12, 25), // Natale
				new DateOnly(seedYear, 12, 26), // Santo Stefano
				new DateOnly(seedYear, 12, 31), // San Silvestro
			];

			DateOnly summerVacationStart = new DateOnly(seedYear, 8, 8); // subito dopo la prima settimana di agosto
			List<DateOnly> summerVacation = Enumerable.Range(0, 14).Select(offset => summerVacationStart.AddDays(offset)).ToList();

			List<ClosedDay> closedDays = weeklyClosures
				.Concat(italianHolidays)
				.Concat(summerVacation)
				.Distinct()
				.Select(d => new ClosedDay { Id = Guid.NewGuid(), Date = d, CreatedBy = adminId })
				.ToList();

			db.ClosedDays.AddRange(closedDays);

			// --- 3. SHIFT ASSIGNMENTS ---
			db.ShiftAssignments.AddRange([
				// mar: luigi (completo) + anna (parziale)
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(tue), EmployeeId = luigiId, StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },
				new ShiftAssignment { Id = annaTueAssignmentId, Date = DateOnly.FromDateTime(tue), EmployeeId = annaId, StartTime = partialStart, Duration = partialDuration, CreatedBy = managerId },

				// mer (oggi): luigi + anna + manager
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(wed), EmployeeId = luigiId,   StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(wed), EmployeeId = annaId,    StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(wed), EmployeeId = managerId, StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },

				// gio: luigi (ha richiesta di cambio pendente) + marco
				new ShiftAssignment { Id = luigiThuAssignmentId, Date = DateOnly.FromDateTime(thu), EmployeeId = luigiId, StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(thu), EmployeeId = marcoId, StartTime = fullStart, Duration = fullDuration, CreatedBy = managerId },

				// ven: manager + anna + marco (parziale)
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(fri), EmployeeId = managerId, StartTime = fullStart, Duration = fullDuration, CreatedBy = adminId },
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(fri), EmployeeId = annaId,    StartTime = fullStart, Duration = fullDuration, CreatedBy = adminId },
				new ShiftAssignment { Id = marcoFriAssignmentId, Date = DateOnly.FromDateTime(fri), EmployeeId = marcoId, StartTime = partialStart, Duration = partialDuration, CreatedBy = adminId },

				// sab: anna (completo) + marco (notturno, chiude alle 02:00 di domenica)
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(sat), EmployeeId = annaId, StartTime = fullStart, Duration = fullDuration, CreatedBy = adminId },
				new ShiftAssignment { Id = Guid.NewGuid(), Date = DateOnly.FromDateTime(sat), EmployeeId = marcoId, StartTime = nightStart, Duration = nightDuration, CreatedBy = adminId },
			]);

			// --- 4. SWAP REQUESTS ---
			// Varietà di stati: Pending, Accepted, Rejected
			db.SwapRequests.AddRange([
				new SwapRequest { // luigi chiede ad anna di coprire il suo gio (pendente, luigi appare evidenziato)
					Id = Guid.NewGuid(),
					ShiftAssignmentId = luigiThuAssignmentId,
					RequesterId = luigiId,
					TargetEmployeeId = annaId,
					Status = SwapRequestStatus.Pending
				},
				new SwapRequest { // anna aveva chiesto a luigi di coprire il suo mar (parziale) → accettato
					Id = Guid.NewGuid(),
					ShiftAssignmentId = annaTueAssignmentId,
					RequesterId = annaId,
					TargetEmployeeId = luigiId,
					Status = SwapRequestStatus.Accepted
				},
				new SwapRequest { // marco aveva chiesto a luigi di coprire il suo ven (parziale) → rifiutato
					Id = Guid.NewGuid(),
					ShiftAssignmentId = marcoFriAssignmentId,
					RequesterId = marcoId,
					TargetEmployeeId = luigiId,
					Status = SwapRequestStatus.Rejected
				},
			]);

			db.SaveChanges();
		}

		// Algoritmo di Meeus/Jones/Butcher per la Pasqua (calendario gregoriano)
		private static DateOnly EasterSunday(int year)
		{
			int a = year % 19;
			int b = year / 100;
			int c = year % 100;
			int d = b / 4;
			int e = b % 4;
			int f = (b + 8) / 25;
			int g = (b - f + 1) / 3;
			int h = (19 * a + b - d - g + 15) % 30;
			int i = c / 4;
			int k = c % 4;
			int l = (32 + 2 * e + 2 * i - h - k) % 7;
			int m = (a + 11 * h + 22 * l) / 451;
			int month = (h + l - 7 * m + 114) / 31;
			int day = ((h + l - 7 * m + 114) % 31) + 1;
			return new DateOnly(year, month, day);
		}
	}
}