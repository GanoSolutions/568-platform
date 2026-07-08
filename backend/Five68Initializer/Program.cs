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
											  // sab 04/07 — nessun turno creato (giorno vuoto)
			DateTime sun = today.AddDays(4);  // dom 05/07 — chiusura

			(Guid shiftMonId, Guid shiftTueId, Guid shiftWedId, Guid shiftThuId, Guid shiftFriId, Guid shiftSunId) = (
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
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

			// --- 2. SHIFTS ---
			// Varietà: chiuso, singolo, multiplo, con parziale, vuoto (sab = nessuna riga)
			db.Shifts.AddRange([
				new Shift { Id = shiftMonId, WorkDate = mon, IsClosed = true,  CreatedBy = adminId   }, // lunedì chiuso
				new Shift { Id = shiftTueId, WorkDate = tue, IsClosed = false, CreatedBy = managerId  }, // 2 dipendenti, 1 parziale
				new Shift { Id = shiftWedId, WorkDate = wed, IsClosed = false, CreatedBy = managerId  }, // oggi, 3 dipendenti
				new Shift { Id = shiftThuId, WorkDate = thu, IsClosed = false, CreatedBy = managerId  }, // cambio pendente
				new Shift { Id = shiftFriId, WorkDate = fri, IsClosed = false, CreatedBy = adminId    }, // 3 dipendenti, 1 parziale
				new Shift { Id = shiftSunId, WorkDate = sun, IsClosed = true,  CreatedBy = adminId    }, // domenica chiusa
			]);

			// --- 3. SHIFT ASSIGNMENTS ---
			db.ShiftAssignments.AddRange([
				// mar: luigi (completo) + anna (parziale)
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftTueId, Date = tue, EmployeeId = luigiId, IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftTueId, Date = tue, EmployeeId = annaId,  IsPartial = true  },

				// mer (oggi): luigi + anna + manager
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftWedId, Date = wed, EmployeeId = luigiId,   IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftWedId, Date = wed, EmployeeId = annaId,    IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftWedId, Date = wed, EmployeeId = managerId, IsPartial = false },

				// gio: luigi (ha richiesta di cambio pendente) + marco
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftThuId, Date = thu, EmployeeId = luigiId, IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftThuId, Date = thu, EmployeeId = marcoId, IsPartial = false },

				// ven: manager + anna + marco (parziale)
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftFriId, Date = fri, EmployeeId = managerId, IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftFriId, Date = fri, EmployeeId = annaId,    IsPartial = false },
				new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftFriId, Date = fri, EmployeeId = marcoId,   IsPartial = true  },
			]);

			// --- 4. SWAP REQUESTS ---
			// Varietà di stati: Pending, Accepted, Rejected
			db.SwapRequests.AddRange([
				new SwapRequest { // luigi chiede ad anna di coprire il suo gio (pendente, luigi appare evidenziato)
					Id = Guid.NewGuid(),
					ShiftId = shiftThuId,
					RequesterId = luigiId,
					TargetEmployeeId = annaId,
					Status = SwapRequestStatus.Pending
				},
				new SwapRequest { // anna aveva chiesto a luigi di coprire mar → accettato
					Id = Guid.NewGuid(),
					ShiftId = shiftTueId,
					RequesterId = annaId,
					TargetEmployeeId = luigiId,
					Status = SwapRequestStatus.Accepted
				},
				new SwapRequest { // marco aveva chiesto a luigi di coprire mer → rifiutato
					Id = Guid.NewGuid(),
					ShiftId = shiftWedId,
					RequesterId = marcoId,
					TargetEmployeeId = luigiId,
					Status = SwapRequestStatus.Rejected
				},
			]);

			db.SaveChanges();
		}
	}
}