using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;

namespace Server.Middleware
{
    public class DeviceAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public DeviceAuthenticationMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only apply device authentication to specific endpoints
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (path.Contains("/api/sensor") || path.Contains("/api/activity/device")))
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (authHeader != null && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader["Bearer ".Length..].Trim();
                    
                    var device = await dbContext.Devices
                        .FirstOrDefaultAsync(d => d.AuthToken == token && d.IsActive);

                    if (device != null)
                    {
                        // Add device info to the context for use in controllers
                        context.Items["Device"] = device;
                        context.Items["DeviceUserId"] = device.UserId;
                    }
                    else
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Invalid device token");
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Device token required");
                    return;
                }
            }

            await _next(context);
        }
    }
}
