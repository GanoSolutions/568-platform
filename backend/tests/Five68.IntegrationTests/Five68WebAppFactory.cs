using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Five68.IntegrationTests;

public class Five68WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureLogging(logging =>
		{
			// Silenzia il rumore Debug/Info del framework (host, AspNetCore, EF Core) durante i
			// test, ma lascia visibili gli Information dei nostri Service/Facade/Controller
			// (namespace Five68), utili per capire cosa succede in un test che fallisce.
			logging.SetMinimumLevel(LogLevel.Warning);
			logging.AddFilter("Five68", LogLevel.Information);
		});
	}

	public async Task InitializeAsync()
	{
		using IServiceScope scope = Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		await db.Database.EnsureCreatedAsync();
	}

	public new async Task DisposeAsync()
	{
		using IServiceScope scope = Services.CreateScope();
		Five68DbContext db = scope.ServiceProvider.GetRequiredService<Five68DbContext>();
		await db.Database.EnsureDeletedAsync();
		await base.DisposeAsync();
	}
}