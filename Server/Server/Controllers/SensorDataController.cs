using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Models;
using System.Globalization;
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
                
                // Get or create activity for this device
                var activity = await GetOrCreateActiveActivity(requestData.UserId, requestData.DeviceId);

                if (activity == null)
                    return StatusCode(500, ApiResponse<string>.Failure("Failed to create or retrieve activity"));
                
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
                        ActivityId = activity.Id,
                        Timestamp = DateTime.UtcNow,
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
                _context.Activities.Update(activity);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Processed {TotalLines} CSV lines, created {TotalPackets} sensor data packets for activity {ActivityId}, {ValidPackets} valid checksums", 
                    lines.Count, sensorDataPackets.Count, activity.Id, validPackets);

                var message = validPackets == sensorDataPackets.Count 
                    ? $"Successfully processed {sensorDataPackets.Count} data packets"
                    : $"Processed {sensorDataPackets.Count} packets, {validPackets} with valid checksums";

                return Ok(ApiResponse<string>.Success("CSV data received", message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV sensor data for {DeviceId}", requestData.DeviceId);
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
                    CurrentTemperature = double.Parse(fields[0].Trim(), CultureInfo.InvariantCulture),
                    CurrentSpeed = double.Parse(fields[1].Trim(), CultureInfo.InvariantCulture),
                    Latitude = double.Parse(fields[2].Trim(), CultureInfo.InvariantCulture),
                    Longitude = double.Parse(fields[3].Trim(), CultureInfo.InvariantCulture),
                    ElevationGain = double.Parse(fields[4].Trim(), CultureInfo.InvariantCulture),
                    AccelerationX = double.Parse(fields[5].Trim(), CultureInfo.InvariantCulture),
                    AccelerationY = double.Parse(fields[6].Trim(), CultureInfo.InvariantCulture),
                    AccelerationZ = double.Parse(fields[7].Trim(), CultureInfo.InvariantCulture),
                    Checksum = double.Parse(fields[8].Trim(), CultureInfo.InvariantCulture)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing CSV line: {Line}", csvLine);
                return null;
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
