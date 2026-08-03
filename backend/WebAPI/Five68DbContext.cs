using Microsoft.EntityFrameworkCore;
using Five68.Models;
using Five68.Models.Authentication;

namespace Five68
{
	public class Five68DbContext : DbContext
	{
		public Five68DbContext(DbContextOptions<Five68DbContext> options)
			: base(options)
		{
		}

		public DbSet<User> Users => Set<User>();
		public DbSet<Employee> Employees => Set<Employee>();
		public DbSet<ClosedDay> ClosedDays => Set<ClosedDay>();
		public DbSet<Shift> Shifts => Set<Shift>();
		public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
		public DbSet<UserRefreshTokens> RefreshTokens => Set<UserRefreshTokens>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<User>(e =>
			{
				e.ToTable("t_users");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).ValueGeneratedNever();
				e.Property(x => x.Role).HasConversion<string>();
				e.HasIndex(x => x.Email).IsUnique();
				e.HasIndex(x => x.InviteToken).IsUnique();
				e.HasOne(x => x.Employee)
					.WithOne()
					.HasForeignKey<Employee>(x => x.UserId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<Employee>(e =>
			{
				e.ToTable("t_employees");
				e.HasKey(x => x.UserId);
				e.Property(x => x.UserId).ValueGeneratedNever();
				e.HasIndex(x => x.FiscalCode).IsUnique();
			});

			modelBuilder.Entity<ClosedDay>(e =>
			{
				e.ToTable("t_closed_days");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).ValueGeneratedNever();
				e.HasIndex(x => x.Date).IsUnique();

				e.HasOne<User>()
					.WithMany()
					.HasForeignKey(x => x.CreatedBy)
					.OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<Shift>(e =>
			{
				e.ToTable("t_shifts");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).ValueGeneratedNever();
				e.HasIndex(x => new { x.Date, x.EmployeeId }).IsUnique();

				e.HasOne<User>()
					.WithMany()
					.HasForeignKey(x => x.CreatedBy)
					.OnDelete(DeleteBehavior.SetNull);

				e.HasOne<Employee>()
					.WithMany()
					.HasForeignKey(x => x.EmployeeId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<SwapRequest>(e =>
			{
				e.ToTable("t_swap_requests");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).ValueGeneratedNever();
				e.Property(x => x.Status).HasConversion<string>();

				e.HasOne(x => x.Shift)
					.WithMany()
					.HasForeignKey(x => x.ShiftId)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasOne<User>()
					.WithMany()
					.HasForeignKey(x => x.RequesterId)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasOne<User>()
					.WithMany()
				 	.HasForeignKey(x => x.TargetEmployeeId)
					.OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<UserRefreshTokens>(e =>
			{
				e.ToTable("t_refresh_tokens");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).ValueGeneratedOnAdd();
				e.HasIndex(x => x.Email);
			});
		}

	}
}