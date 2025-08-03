using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Server.Data;
using Server.Models;
using Server.Services;
using System.Text;
using System.IO;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            var env = builder.Environment;
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information) // allow normal info
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code);

            Log.Logger = loggerConfig.CreateLogger();

            builder.Host.UseSerilog();

            Console.WriteLine(Directory.GetCurrentDirectory());

            // Add Entity Framework Core
            var connectionString = builder.Environment.IsDevelopment() 
                ? "Data Source=../Server/Data/app.db"  // Development path
                : "Data Source=/app/Data/app.db";      // Production/Docker path
            
            Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
            
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));

            // Add Identity services
            builder.Services.AddIdentity<User, Role>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.SignIn.RequireConfirmedEmail = false; // For simplicity, disable email confirmation
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Configure Data Protection to use persistent storage (production only)
            if (!builder.Environment.IsDevelopment())
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo("/app/Data/DataProtection-Keys"));
            }

            // Add Mqtt Service
            builder.Services.Configure<MqttClientOptions>(builder.Configuration.GetRequiredSection("MQTT"));
            builder.Services.AddHostedService<MqttService>();

            // Configure JWT Authentication
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidAudience = Constants.JwtAudience,
                        ValidateIssuer = true,
                        ValidIssuer = Constants.JwtIssuer,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetRequiredSection("JwtKey").Value!)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
            
            // Add Weather Services
            builder.Services.Configure<ApiOptions>(
                builder.Configuration.GetSection("Apis"));

            // Register HttpClient for injection
            builder.Services.AddHttpClient();

            // Register WeatherService as singleton to keep cache alive
            builder.Services.AddSingleton<WeatherService>();

            // Add Authorization
            builder.Services.AddAuthorization();

            // Add Mud Blazor Services
            builder.Services.AddMudServices();
            builder.Services.AddLocalization();
            builder.Services.AddMudLocalization();

            // Add HTTP Client for server-side AuthService (for prerendering)
            builder.Services.AddHttpClient();

            // Add Auth Service here too because idk, i assume prerendering
            builder.Services.AddScoped<Frontend.Client.Services.AuthService>();
            builder.Services.AddScoped<Frontend.Client.Services.JwtAuthenticationStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
                provider.GetRequiredService<Frontend.Client.Services.JwtAuthenticationStateProvider>());

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents();

            // web api stuff
            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    // Disable automatic 400 responses for validation errors
                    // so we can handle them manually in controllers
                    options.SuppressModelStateInvalidFilter = true;
                });

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.WebHost.UseWebRoot("wwwroot");
            builder.WebHost.UseStaticWebAssets();

            var app = builder.Build();

            // Ensure database adds all migrations
            using (var scope = app.Services.CreateScope())
            {
                try 
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    context.Database.Migrate();
                    Log.Information("Database migration completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Database migration failed. Connection string: {ConnectionString}", connectionString);
                    throw;
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
                app.MapOpenApi();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.MapStaticAssets();

            app.MapControllers();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapRazorComponents<Components.App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Frontend.Client.Services.AuthService).Assembly)
                .AllowAnonymous();

            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}
