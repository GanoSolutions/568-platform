using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Web;
using Easy_Password_Validator;
using Five68.Facades;
using Five68.Services;
using Microsoft.Extensions.Options;
using Five68.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Reflection;
using System.Text.Json.Nodes;


namespace Five68
{
	public class Program
	{
		public static void Main(string[] args)
		{
			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			builder.Configuration
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables();

			InitializeLogger();

			LogManager.GetCurrentClassLogger().Debug(
				"Environment: {0} | Loaded config keys: {1}",
				builder.Environment.EnvironmentName,
				string.Join(", ", builder.Configuration.AsEnumerable()
					.Where(kv => kv.Key.StartsWith("Logging"))
					.Select(kv => $"{kv.Key}={kv.Value}"))
			);

			LoadServicesIntoDI(builder.Services, builder.Configuration);

			builder.Services.AddHttpContextAccessor();
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new OpenApiInfo { Title = "568 Platform APIs", Version = "v1" });
				c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme()
				{
					Type = SecuritySchemeType.Http,
					Scheme = "bearer",
					BearerFormat = "JWT",
					Description = "JWT Authorization header using the Bearer scheme"
				});

				c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
				{
					[new OpenApiSecuritySchemeReference("bearer", document)] = []
				});
				String xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
				c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

				// produce a correct example in swagger
				c.MapType<TimeOnly>(() => new OpenApiSchema
				{
					Type = JsonSchemaType.String,
					Format = "time",
					Example = JsonValue.Create("13:45:00"),
				});
				c.MapType<TimeSpan>(() => new OpenApiSchema
				{
					Type = JsonSchemaType.String,
					Example = JsonValue.Create("08:00:00"),
					Description = "Duration in [d.]hh:mm:ss format (e.g. \"08:00:00\" for 8 hours, \"1.00:00:00\" for 24 hours).",
				});
			});

			// Configure the HTTP request pipeline
			builder.Services.AddCors(options =>
			{
				options.AddDefaultPolicy(builder =>
				{
					builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
				});
			});

			WebApplication app = builder.Build();
			if (app.Environment.IsDevelopment())
			{
				app.UseSwagger();
				app.UseSwaggerUI();
			}
			else
			{
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseCors();
			app.UseAuthentication();
			app.UseAuthorization();
			// app.UseResponseCompression();

			app.UseMiddleware<ExceptionMiddleware>();
			app.MapControllers();
			app.UseStaticFiles(new StaticFileOptions
			{
				RequestPath = "/static",
			});

			Microsoft.Extensions.Logging.ILogger logger = app.Services.GetRequiredService<ILogger<Program>>();

			app.Run();

			logger.LogCritical("Shutting down");
			LogManager.Shutdown();
		}

		public static void LoadServicesIntoDI(IServiceCollection services, IConfigurationRoot configuration)
		{
			// Settings
			services.Configure<AppSettings>(configuration.GetSection(AppSettings.Position));

			JWTSettings jwtSettings = configuration.GetSection(AppSettings.Position)
				.GetSection("JWTSettings")
				.Get<JWTSettings>()!;

			services.AddAuthentication(x =>
			{
				x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(options =>
				{
					options.SaveToken = true;
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuer = jwtSettings.ValidateIssuer,
						ValidateAudience = jwtSettings.ValidateAudience,
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						ValidIssuer = jwtSettings.ValidIssuer,
						ValidAudience = jwtSettings.ValidAudience,
						IssuerSigningKey = new SymmetricSecurityKey(
							Encoding.UTF8.GetBytes(jwtSettings.Secret)),
						ClockSkew = TimeSpan.Zero,
					};
				});

			// Database
			services.AddDbContext<Five68DbContext>((sp, options) =>
			{
				options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
			});

			// Facades
			services.AddScoped<RefreshTokenFacade>();
			services.AddScoped<UserFacade>();
			services.AddScoped<EmployeeFacade>();
			services.AddScoped<ShiftFacade>();

			// Services
			services.AddScoped<AuthService>();
			services.AddSingleton<JwtService>();
			services.AddScoped<UserService>();
			services.AddScoped<IEmailService, NoOpEmailService>();
			services.AddScoped<ShiftService>();

			// Utils
			services.AddSingleton<UserUtils>();

			// Password Validator
			services.AddSingleton(sp =>
			{
				AppSettings settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
				return new PasswordValidatorService(settings.PasswordRequirements);
			});

			// Logging
			services.AddLogging(loggingBuilder =>
			{
				loggingBuilder.ClearProviders();
				loggingBuilder.AddNLog(new NLogAspNetCoreOptions() { RemoveLoggerFactoryFilter = false });
			});

			// Controllers
			services.AddControllers();
		}

		private static void InitializeLogger()
		{
			LoggingConfiguration config = new();
			LogManager.Setup().RegisterNLogWeb();

			ColoredConsoleTarget consoleTarget = new()
			{
				Name = "Console",
				Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=ToString}",
			};
			config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, consoleTarget, "*");
			LogManager.Configuration = config;
		}
	}
}