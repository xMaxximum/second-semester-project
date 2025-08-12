using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Models;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.DefaultRoute)]
    public class SensorDataController: ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SensorDataController> _logger;

        public SensorDataController(ApplicationDbContext context, ILogger<SensorDataController> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        [HttpPost("start-activity")]
        public async Task<ActionResult<ApiResponse<ActivityResponse>>> CreateActivity(ActivityCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ActivityResponse>.Failure("Validation failed", errors));
                }

                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(ApiResponse<ActivityResponse>.Failure("User not found"));

                var activity = new Activity
                {
                    UserId = userId.Value,
                    Name = request.Name,
                    Description = request.Description,
                    StartTime = request.StartTime ?? DateTime.UtcNow,
                    Status = ActivityStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Activities.Add(activity);
                await _context.SaveChangesAsync();

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
                    DataPacketCount = 0,
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt
                };

                _logger.LogInformation("Activity {ActivityId} created for user {UserId}", activity.Id, userId);
                return Ok(ApiResponse<ActivityResponse>.Success(response, "Activity created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating activity");
                return StatusCode(500, ApiResponse<ActivityResponse>.Failure("Internal server error"));
            }
        }

        [HttpPost("data")]
        public async Task<ActionResult<ApiResponse<SensorDataPacketResponse>>> AddSensorData(
            [FromBody] SensorDataPacketRequest requestData)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ActivityResponse>.Failure("Validation failed", errors));
                }

              // Verify activity exists and is active
                var activity = await _context.Activities
                    .FirstOrDefaultAsync(a => a.Id == requestData.ActivityId);

                if (activity == null)
                    return NotFound(ApiResponse<string>.Failure("Activity not found"));

                if (activity.Status != ActivityStatus.InProgress)
                    return BadRequest(ApiResponse<string>.Failure("Activity is not in progress"));

                // Parse CSV data
                var lines = requestData.CsvData
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                
                var sensorDataPackets = new List<SensorDataPacket>();
                var validPackets = 0;
                var totalPackets = 0;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                    var dataPoint = ParseCsvLine(trimmedLine);
                    if (dataPoint == null)
                    {
                        _logger.LogWarning("Failed to parse CSV line: {Line}", trimmedLine);
                        continue;
                    }

                    totalPackets++;
                    
                    var sensorPacket = new SensorDataPacket
                    {
                        ActivityId = requestData.ActivityId,
                        Timestamp = DateTime.UtcNow,
                        TimeSinceStart = "PT0S",  // need to figure this out
                        CurrentTemperature = dataPoint.CurrentTemperature,
                        CurrentSpeed = dataPoint.CurrentSpeed,
                        Latitude = dataPoint.Latitude,
                        Longitude = dataPoint.Longitude,
                        ElevationGain = dataPoint.ElevationGain,
                        AccelerationX = dataPoint.AccelerationX,
                        AccelerationY = dataPoint.AccelerationY,
                        AccelerationZ = dataPoint.AccelerationZ,
                        Checksum = dataPoint.Checksum,
                        DeviceId = requestData.DeviceId,
                        CreatedAt = DateTime.UtcNow
                    };
                    sensorPacket.IsChecksumValid = sensorPacket.ValidateChecksum();
                    if (sensorPacket.IsChecksumValid)
                    {
                        sensorDataPackets.Add(sensorPacket);
                        validPackets++;
                    }
                }

                if (sensorDataPackets.Count == 0)
                {
                    return BadRequest(ApiResponse<string>.Failure("No valid data points found in CSV"));
                }

                // add all packets to database
                _context.SensorDataPackets.AddRange(sensorDataPackets);
                activity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Processed {TotalLines} CSV lines, created {TotalPackets} sensor data packets for activity {ActivityId}, {ValidPackets} valid checksums", 
                    lines.Count, sensorDataPackets.Count, requestData.ActivityId, validPackets);

                var message = validPackets == sensorDataPackets.Count 
                    ? $"Successfully processed {sensorDataPackets.Count} data packets"
                    : $"Processed {sensorDataPackets.Count} packets, {validPackets} with valid checksums";

                return Ok(ApiResponse<string>.Success("CSV data received", message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV sensor data for activity {ActivityId}", requestData.ActivityId);
                return StatusCode(500, ApiResponse<string>.Failure("Internal server error"));
            }
        }
        
        //method for parsing csv lines
        private CsvDataPoint? ParseCsvLine(string csvLine)
        {
            try
            {
                var fields = csvLine.Split(',');
                
                if (fields.Length < 9)
                {
                    _logger.LogWarning("CSV line has insufficient fields: {Line}", csvLine);
                    return null;
                }

                return new CsvDataPoint
                {
                    CurrentTemperature = double.Parse(fields[0].Trim()),
                    CurrentSpeed = double.Parse(fields[1].Trim()),
                    Latitude = double.Parse(fields[2].Trim()),
                    Longitude = double.Parse(fields[3].Trim()),
                    ElevationGain = double.Parse(fields[4].Trim()), 
                    AccelerationX = double.Parse(fields[5].Trim()),
                    AccelerationY = double.Parse(fields[6].Trim()),
                    AccelerationZ = double.Parse(fields[7].Trim()),
                    Checksum = double.Parse(fields[8].Trim())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing CSV line: {Line}", csvLine);
                return null;
            }
        }
        
        private async Task<Activity> GetOrCreateActiveActivity(string deviceId)
        {
            // First, try to find an existing active activity for this device
            var existingActivity = await _context.Activities
                .Where(a => a.Status == ActivityStatus.InProgress)
                .Include(a => a.SensorDataPackets)
                .FirstOrDefaultAsync(a => a.SensorDataPackets.Any(s => s.DeviceId == deviceId));

            if (existingActivity != null)
            {
                return existingActivity;
            }

            // Create new activity
            var newActivity = new Activity
            {
                UserId = 1, // Default user - you might want to implement device-user mapping
                Name = $"Cycling Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                Description = $"Automatic session from device {deviceId}",
                StartTime = DateTime.UtcNow,
                Status = ActivityStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Activities.Add(newActivity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new activity {ActivityId} for device {DeviceId}", newActivity.Id, deviceId);
            return newActivity;
        }
        
        private long? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
