using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Models;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.DefaultRoute + "/sensordata")]
    public class SensorDataController: ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SensorDataController> _logger;

        public SensorDataController(ApplicationDbContext context, ILogger<SensorDataController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("activities/{activityId}/data")]
        public async Task<ActionResult<ApiResponse<SensorDataPacketResponse>>> AddSensorData(
            long activityId,
            [FromBody] SensorDataPacketRequest requestData,
            [FromHeader(Name = "Devide-ID")] string? deviceId = null)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ActivityResponse>.Failure("Validation failed", errors));
                }
                
                var activity = await _context.Activities.FindAsync(activityId);

                if (activity == null)
                {
                    _logger.LogError($"Activity with id {activityId} not found");
                    return NotFound(ApiResponse<SensorDataPacketResponse>.Failure("Activity with id {activityId} not found"));
                }
                
                // check if a device belongs to the userId
                if (!string.IsNullOrEmpty(deviceId))
                {
                    var device = await _context.Devices.
                        FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == activity.UserId);
                    
                    if (device == null)
                    {
                        _logger.LogWarning("Device {DeviceId} attempted to add data to activity {ActivityId} but doesn't belong to the same user", 
                            deviceId, activityId);
                        return Unauthorized(ApiResponse<SensorDataPacketResponse>.Failure("Device not authorized for this activity"));
                    }
                }

                var sensorData = new SensorDataPacket
                {
                    ActivityId = activityId,
                    Timestamp = DateTime.UtcNow,
                    TimeSinceStart = requestData.TimeSinceStart,
                    CurrentTemperature = requestData.CurrentTemperature,
                    CurrentSpeed = requestData.CurrentSpeed,
                    Latitude = requestData.Latitude,
                    Longitude = requestData.Longitude,
                    AveragedAccelerationX = requestData.AveragedAccelerationX,
                    AveragedAccelerationY = requestData.AveragedAccelerationY,
                    AveragedAccelerationZ = requestData.AveragedAccelerationZ,
                    PeakAccelerationX = requestData.PeakAccelerationX,
                    PeakAccelerationY = requestData.PeakAccelerationY,
                    PeakAccelerationZ = requestData.PeakAccelerationZ,
                    Checksum = requestData.Checksum,
                    DeviceId = deviceId,
                    CreatedAt = DateTime.UtcNow,
                };
                
                // validate checksum of package 
                sensorData.IsChecksumValid = sensorData.ValidateChecksum();

                if (!sensorData.IsChecksumValid)
                {
                    _logger.LogWarning("Invalid checksum received for activity {ActivityId} from device {DeviceId}. Expected: {Expected}, Received: {Received}", 
                        activityId, deviceId, sensorData.Checksum, requestData.Checksum);
                }
                
                // add to database and update database
                _context.SensorDataPackets.Add(sensorData); 
                activity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                // Create response
                var response = new SensorDataPacketResponse
                {
                    Id = sensorData.Id,
                    ActivityId = sensorData.ActivityId,
                    Timestamp = sensorData.Timestamp,
                    TimeSinceStart = sensorData.TimeSinceStart,
                    CurrentTemperature = sensorData.CurrentTemperature,
                    CurrentSpeed = sensorData.CurrentSpeed,
                    Latitude = sensorData.Latitude,
                    Longitude = sensorData.Longitude,
                    AveragedAccelerationX = sensorData.AveragedAccelerationX,
                    AveragedAccelerationY = sensorData.AveragedAccelerationY,
                    AveragedAccelerationZ = sensorData.AveragedAccelerationZ,
                    PeakAccelerationX = sensorData.PeakAccelerationX,
                    PeakAccelerationY = sensorData.PeakAccelerationY,
                    PeakAccelerationZ = sensorData.PeakAccelerationZ,
                    Checksum = sensorData.Checksum,
                    IsChecksumValid = sensorData.IsChecksumValid,
                    DeviceId = sensorData.DeviceId,
                    TotalAcceleration = sensorData.TotalAcceleration,
                    TotalPeakAcceleration = sensorData.TotalPeakAcceleration
                };

                _logger.LogDebug("Sensor data added to activity {ActivityId} from device {DeviceId}. Checksum valid: {IsValid}", 
                    activityId, deviceId ?? "unknown", sensorData.IsChecksumValid);

                return Ok(ApiResponse<SensorDataPacketResponse>.Success(response, "Sensor data added successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding sensor data to activity {ActivityId} from device {DeviceId}", activityId, deviceId);
                return StatusCode(500, ApiResponse<SensorDataPacketResponse>.Failure("Internal server error"));
            }
        }
    }
}
