using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http;
using MudBlazor.Services;
using Frontend.Client.Services;

namespace Frontend.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // Add MudBlazor services
            builder.Services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
            });
            builder.Services.AddLocalization();
            builder.Services.AddMudLocalization();

            // Add Authorization Core
            builder.Services.AddAuthorizationCore();

            // Add Authentication State Provider
            builder.Services.AddSingleton<JwtAuthenticationStateProvider>();
            builder.Services.AddSingleton<AuthenticationStateProvider>(provider =>
                provider.GetRequiredService<JwtAuthenticationStateProvider>());

            // Configure HTTP Client with JWT handler
            var appUri = new Uri(builder.HostEnvironment.BaseAddress);
            builder.Services.AddScoped(provider => new JwtTokenMessageHandler(appUri,
                provider.GetRequiredService<JwtAuthenticationStateProvider>()));

            builder.Services.AddHttpClient("Server.API", client => client.BaseAddress = appUri)
                .AddHttpMessageHandler<JwtTokenMessageHandler>();

            builder.Services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Server.API"));

            // Add Auth Service
            builder.Services.AddScoped<AuthService>();

            var app = builder.Build();

            // Try to refresh token on startup
            await RefreshJwtToken(app);

            await app.RunAsync();
        }

        private static async Task RefreshJwtToken(WebAssemblyHost application)
        {
            try
            {
                using var scope = application.Services.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                await authService.RefreshTokenAsync();
            }
            catch
            {
                // Silently fail - user will need to login manually
            }
        }
    }
}
