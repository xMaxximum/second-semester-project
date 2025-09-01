using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Models;
using System.Globalization;

namespace Server.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route(Constants.RoutePrefix + "/sensor")]
    public class SensorDataController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SensorDataController> _logger;

        public SensorDataController(ApplicationDbContext context, ILogger<SensorDataController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("stop-activity")]
        public async Task<ActionResult<ApiResponse<ActivityResponse>>> StopActivity(
            [FromBody] StopActivityRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ActivityResponse>.Failure("Validation failed", errors));
                }

                // Get device and user ID from device authentication middleware
                var device = HttpContext.Items["Device"] as Device;
                var deviceUserId = HttpContext.Items["DeviceUserId"] as long?;

                if (device == null || deviceUserId == null)
                {
                    return Unauthorized(ApiResponse<ActivityResponse>.Failure("Device authentication required"));
                }
                
                // find active activity for user
                var activity = await _context.Activities
                    .Include(a => a.SensorDataPackets)
                    .FirstOrDefaultAsync(a => a.UserId == deviceUserId && a.Status == ActivityStatus.InProgress);

                if (activity == null)
                {
                    return NotFound(ApiResponse<ActivityResponse>.Failure("No active activity found for this device"));
                }

                // stop the activity
                activity.EndTime = DateTime.UtcNow;
                activity.Status = ActivityStatus.Completed;
                activity.UpdatedAt = DateTime.UtcNow;

                _context.Activities.Update(activity);
                await _context.SaveChangesAsync();

                // Create response
                var response = new ActivityResponse
                {
                    Id = activity.Id,
                    Name = activity.Name,
                    Description = activity.Description,
                    StartTime = activity.StartTime,
                    EndTime = activity.EndTime,
                    Status = activity.Status,
                    Duration = activity.Duration,
                    IsActive = activity.IsActive,
                    DataPacketCount = activity.DataPacketCount,
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt
                };

                _logger.LogInformation("Activity {ActivityId} stopped by device {DeviceId}", activity.Id, device.DeviceId);
                return Ok(ApiResponse<ActivityResponse>.Success(response, "Activity stopped successfully"));
            }
            catch (Exception ex)
            {
                var device = HttpContext.Items["Device"] as Device;
                _logger.LogError(ex, "Error stopping activity for device {DeviceId}", device?.DeviceId ?? "unknown");
                return StatusCode(500, ApiResponse<ActivityResponse>.Failure("Internal server error"));
            }
        }
        
        [HttpPost("data")]
        public async Task<ActionResult<ApiResponse<SensorDataPacketResponse>>> AddSensorData()
        {
            try
            {
                // I have sent the data as a raw float array over http
                using var memoryStream = new MemoryStream();
                await Request.Body.CopyToAsync(memoryStream);

                // is the binarydata divisible by 4 and therefore consisting out of floats?
                if (memoryStream.Length % 4 != 0)
                {
                    return BadRequest(ApiResponse<string>.Failure("Invalid binary data size; not divisible by 4."));
                }

                memoryStream.Position = 0;
                int floatCount = (int)(memoryStream.Length / 4);

                // read the binary data that is a float array
                var floatValues = new float[floatCount];
                using var reader = new BinaryReader(memoryStream);
                for (int i = 0; i < floatCount; i++)
                {
                    floatValues[i] = reader.ReadSingle();
                }

                // this is the corresponding device and userid to the authToken send by the esp
                // the authToken is wrong when there is no user or device for that token registered
                var device = HttpContext.Items["Device"] as Device;
                var deviceUserId = HttpContext.Items["DeviceUserId"] as long?;

                if (device == null || deviceUserId == null)
                    return Unauthorized(ApiResponse<string>.Failure("Device authentication required"));
                

                // Get or create activity for this device - use the device's user ID
                var activity = await GetOrCreateActiveActivity(deviceUserId.Value, device.DeviceId);
                if (activity == null)
                    return StatusCode(500, ApiResponse<string>.Failure("Failed to create or retrieve activity"));
                if (activity.Status != ActivityStatus.InProgress)
                    return BadRequest(ApiResponse<string>.Failure("Activity is not in progress"));
                
                // processing the float array here and saving each sensordata packet into a sensorPacket object
                const int floatsPerPacket = 9;
                var sensorDataPackets = new List<SensorDataPacket>();
                var validCount = 0;

                for (int i = 0; i < floatValues.Length; i += floatsPerPacket)
                {
                    if (i + floatsPerPacket > floatValues.Length)
                    {
                        // data is corrupt when there are more floats that do not form a complete packet, ignore those
                        break;
                    }

                    // map each packet from the array to the object
                    var sensorPacket = new SensorDataPacket
                    {
                        ActivityId = activity.Id,
                        Timestamp = DateTime.UtcNow,
                        CurrentTemperature = floatValues[i + 0],
                        CurrentSpeed       = floatValues[i + 1],
                        Latitude           = floatValues[i + 2],
                        Longitude          = floatValues[i + 3],
                        CurrentElevation      = floatValues[i + 4],
                        AccelerationX      = floatValues[i + 5],
                        AccelerationY      = floatValues[i + 6],
                        AccelerationZ      = floatValues[i + 7],
                        Checksum           = floatValues[i + 8],
                        DeviceId           = device.DeviceId,
                        CreatedAt          = DateTime.UtcNow
                    };

                    // Validate checksum if needed
                    sensorPacket.IsChecksumValid = sensorPacket.ValidateChecksum();
                    if (sensorPacket.IsChecksumValid)
                    {
                        sensorDataPackets.Add(sensorPacket);
                        validCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Checksum not valid in one of the lines");
                        // error handling logic down the line
                    }
                }

                if (sensorDataPackets.Count == 0)
                {
                    return BadRequest(ApiResponse<string>.Failure("No valid data packets found"));
                }

                // add all packets to database
                _context.SensorDataPackets.AddRange(sensorDataPackets);
                activity.UpdatedAt = DateTime.UtcNow;
                _context.Activities.Update(activity);
                await _context.SaveChangesAsync();

                // changed logger messages, because it is float data and not csv strings anymore
                _logger.LogInformation(
                    "Processed {FloatCount} floats, created {PacketCount} packets, {ValidCount} valid checksums",
                    floatValues.Length, sensorDataPackets.Count, validCount);

                var message = validCount == sensorDataPackets.Count
                    ? $"Successfully processed {sensorDataPackets.Count} data packets."
                    : $"Processed {sensorDataPackets.Count} packets, {validCount} valid checksums.";

                return Ok(ApiResponse<string>.Success("Binary float data received", message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing octet-stream data.");
                return StatusCode(500, ApiResponse<string>.Failure("Internal server error"));
            }
        }
        
        private async Task<Activity?> GetOrCreateActiveActivity(long userId, string deviceId)
        {
            try
            {
                // First, try to find an existing active activity for this device
                var existingActivity = await _context.Activities
                    .Where(a => a.UserId == userId && a.Status == ActivityStatus.InProgress)
                    .Include(a => a.SensorDataPackets)
                    .FirstOrDefaultAsync();

                if (existingActivity != null)
                {
                    return existingActivity;
                }

                // Create new activity
                var newActivity = new Activity
                {
                    UserId = userId,
                    DeviceId = deviceId,
                    Name = $"Session from device {deviceId}",
                    StartTime = DateTime.UtcNow,
                    Status = ActivityStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Activities.Add(newActivity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new activity {ActivityId} for device {DeviceId} and user {UserId}", 
                    newActivity.Id, deviceId, userId);
                
                return newActivity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating activity for device {DeviceId}", deviceId);
                return null;
            }
        }
    }
}
