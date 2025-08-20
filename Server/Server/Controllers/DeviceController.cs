using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using Shared.Models;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<DeviceController> _logger;
        
        // Static dictionary to store temporary activation codes (in production, use Redis or similar)
        private static readonly ConcurrentDictionary<string, TempActivationInfo> TempActivationCodes = new();

        public DeviceController(ApplicationDbContext context, UserManager<User> userManager, ILogger<DeviceController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<DeviceDto>>> GetDevices()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var devices = await _context.Devices
                .Where(d => d.UserId == userId)
                .Select(d => new DeviceDto
                {
                    DeviceId = d.DeviceId,
                    Name = d.Name,
                    FirmwareVersion = d.FirmwareVersion ?? "1.0",
                    RegisteredAt = d.RegisteredAt,
                    IsActive = d.IsActive,
                    HasAuthToken = !string.IsNullOrEmpty(d.AuthToken),
                    HasPendingActivation = !string.IsNullOrEmpty(d.ActivationCode) && d.ActivationCodeExpiry > DateTime.UtcNow,
                    ActivationCode = d.ActivationCode,
                    ActivationCodeExpiry = d.ActivationCodeExpiry,
                    AuthToken = d.AuthToken
                })
                .ToListAsync();

            return Ok(devices);
        }



        [HttpPost("generate-temp-activation-code")]
        [Authorize]
        public ActionResult<object> GenerateTempActivationCode()
        {
            // Generate a 6-digit activation code without creating a device
            var activationCode = GenerateSecureActivationCode();
            var expiry = DateTime.UtcNow.AddMinutes(10); // Code expires in 10 minutes

            // Store temporarily (you could use a cache like Redis, but for now we'll use a static dictionary)
            TempActivationCodes[activationCode] = new TempActivationInfo
            {
                UserId = GetCurrentUserId()!.Value,
                ExpiresAt = expiry,
                Connected = false
            };

            return Ok(new
            {
                ActivationCode = activationCode,
                ExpiresAt = expiry,
                ExpiresInMinutes = 10
            });
        }

        [HttpGet("check-temp-activation/{activationCode}")]
        [Authorize]
        public ActionResult<object> CheckTempActivation(string activationCode)
        {
            if (TempActivationCodes.TryGetValue(activationCode, out var tempInfo))
            {
                var userId = GetCurrentUserId();
                if (tempInfo.UserId != userId) return Forbid();

                return Ok(new
                {
                    tempInfo.Connected,
                    tempInfo.ExpiresAt
                });
            }

            return NotFound();
        }

        [HttpPost("complete-activation")]
        [Authorize]
        public async Task<ActionResult<DeviceDto>> CompleteActivation([FromBody] CompleteActivationRequest request)
        {
            if (!TempActivationCodes.TryGetValue(request.ActivationCode, out var tempInfo))
            {
                return BadRequest("Invalid or expired activation code");
            }

            var userId = GetCurrentUserId();
            if (tempInfo.UserId != userId) return Forbid();

            if (!tempInfo.Connected)
            {
                return BadRequest("Device has not connected yet");
            }

            // Find the existing device that was created during registration and update its name
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.DeviceId == tempInfo.DeviceId && d.UserId == userId);

            if (device == null)
            {
                return BadRequest("Device not found. Please ensure the device has completed registration.");
            }

            // Update the device name
            device.Name = request.DeviceName;
            await _context.SaveChangesAsync();

            // Clean up temp activation code
            TempActivationCodes.TryRemove(request.ActivationCode, out _);

            var deviceDto = new DeviceDto
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                FirmwareVersion = device.FirmwareVersion ?? "1.0",
                RegisteredAt = device.RegisteredAt,
                IsActive = device.IsActive,
                HasAuthToken = !string.IsNullOrEmpty(device.AuthToken),
                HasPendingActivation = false,
                AuthToken = device.AuthToken
            };

            return Ok(deviceDto);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationCode) || string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest("Activation code and device ID are required");

            // Check if it's a temporary activation code first
            if (TempActivationCodes.TryGetValue(request.ActivationCode, out var tempInfo))
            {
                if (tempInfo.ExpiresAt < DateTime.UtcNow)
                {
                    TempActivationCodes.TryRemove(request.ActivationCode, out _);
                    return BadRequest("Activation code has expired");
                }

                // Generate auth token and create device immediately with temporary name
                var authToken = GenerateSecureAuthToken();
                var device = new Device
                {
                    DeviceId = request.DeviceId,
                    Name = $"Device {request.DeviceId.Substring(0, Math.Min(8, request.DeviceId.Length))}", // Temporary name
                    UserId = tempInfo.UserId,
                    AuthToken = authToken,
                    IsActive = true,
                    RegisteredAt = DateTime.UtcNow
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                // Mark as connected and store the device info
                tempInfo.Connected = true;
                tempInfo.DeviceId = request.DeviceId;
                
                _logger.LogInformation("Device {DeviceId} registered with temp activation code", 
                    request.DeviceId);

                // Return auth token to device immediately
                return Ok(new { 
                    DeviceId = device.DeviceId,
                    AuthToken = authToken,
                    Message = "Device registered successfully. You can now use this token for API requests."
                });
            }

            // Fallback to old flow for existing activation codes in database
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.ActivationCode == request.ActivationCode && 
                                         d.ActivationCodeExpiry > DateTime.UtcNow);

            if (existingDevice == null)
            {
                _logger.LogWarning("Invalid activation code attempt, Code: {ActivationCode}", 
                    request.ActivationCode);
                return BadRequest("Invalid or expired activation code");
            }

            // Generate permanent auth token
            var permanentAuthToken = GenerateSecureAuthToken();

            // Update device
            existingDevice.AuthToken = permanentAuthToken;
            existingDevice.ActivationCode = null; // Clear activation code
            existingDevice.ActivationCodeExpiry = null;
            existingDevice.IsActive = true;

            // If the device provided a hardware device ID, update it
            if (!string.IsNullOrWhiteSpace(request.DeviceId))
            {
                existingDevice.DeviceId = request.DeviceId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Device registered successfully: {DeviceId} for User: {UserId}", 
                existingDevice.DeviceId, existingDevice.UserId);

            return Ok(new
            {
                DeviceId = existingDevice.DeviceId,
                AuthToken = permanentAuthToken,
                Message = "Device registered successfully"
            });
        }

        [HttpPut("{deviceId}")]
        [Authorize]
        public async Task<IActionResult> UpdateDevice(string deviceId, [FromBody] UpdateDeviceRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId);

            if (device == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(request.Name))
                device.Name = request.Name;

            if (request.IsActive.HasValue)
                device.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                device.DeviceId,
                device.Name,
                device.FirmwareVersion,
                device.RegisteredAt,
                device.IsActive
            });
        }

        [HttpDelete("{deviceId}")]
        [Authorize]
        public async Task<IActionResult> DeleteDevice(string deviceId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId);

            if (device == null)
                return NotFound();

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Device deleted successfully" });
        }



        [HttpGet("{deviceId}/status")]
        [Authorize]
        public async Task<ActionResult<DeviceDto>> GetDeviceStatus(string deviceId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId);

            if (device == null)
                return NotFound();

            var deviceDto = new DeviceDto
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                FirmwareVersion = device.FirmwareVersion ?? "1.0",
                RegisteredAt = device.RegisteredAt,
                IsActive = device.IsActive,
                HasAuthToken = !string.IsNullOrEmpty(device.AuthToken),
                HasPendingActivation = !string.IsNullOrEmpty(device.ActivationCode) && device.ActivationCodeExpiry > DateTime.UtcNow,
                ActivationCode = device.ActivationCode,
                ActivationCodeExpiry = device.ActivationCodeExpiry,
                AuthToken = device.AuthToken
            };

            return Ok(deviceDto);
        }

        private long? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private string GenerateSecureActivationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
            return number.ToString("D6"); // 6-digit code with leading zeros
        }

        private string GenerateSecureAuthToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[64];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class RegisterDeviceRequest
    {
        public string ActivationCode { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty; // Optional: Hardware device ID from ESP32
    }

    public class UpdateDeviceRequest
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
    }

    public class CompleteActivationRequest
    {
        public string ActivationCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class TempActivationInfo
    {
        public long UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Connected { get; set; }
        public string? DeviceId { get; set; }
    }
}
