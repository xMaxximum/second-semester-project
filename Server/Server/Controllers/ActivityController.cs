using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Server.Data;
using Server.Models;
using Shared.Models;
using System.Security.Claims;
using System.Text.Json;
using System.Xml;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.RoutePrefix + "/activities")]
    public class ActivityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityController> _logger;
        private readonly IWebHostEnvironment _env;

        public ActivityController(ApplicationDbContext context, ILogger<ActivityController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<ActivityListResponse>> GetActivities(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] ActivityStatus? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ActivityListResponse { IsSuccess = false, Message = "User not found" });

                var query = _context.Activities
                    .Where(a => a.UserId == userId.Value);

                if (status.HasValue)
                    query = query.Where(a => a.Status == status.Value);

                var totalCount = await query.CountAsync();
                
                var activityEntities = await query
                    .Include(a => a.SensorDataPackets)
                    .OrderByDescending(a => a.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var activities = activityEntities.Select(a => new ActivityResponse
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status,
                    Duration = a.EndTime.HasValue ? a.EndTime.Value - a.StartTime : null,
                    IsActive = a.Status == ActivityStatus.InProgress,
                    DataPacketCount = a.SensorDataPackets.Count,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    Analytics = CalculateAnalytics(a.SensorDataPackets.OrderBy(s => s.Timestamp).ToList())
                }).ToList();

                return Ok(new ActivityListResponse
                {
                    IsSuccess = true,
                    Message = "Activities retrieved successfully",
                    Activities = activities,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activities");
                return StatusCode(500, new ActivityListResponse
                {
                    IsSuccess = false,
                    Message = "Internal server error"
                });
            }
        }

        // GET: api/activities/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ActivityDetailsResponse>> GetActivity(long id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new ActivityDetailsResponse { IsSuccess = false, Message = "User not found" });

                var activity = await _context.Activities
                    .Include(a => a.SensorDataPackets.OrderBy(s => s.Timestamp))
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

                if (activity == null)
                    return NotFound(new ActivityDetailsResponse { IsSuccess = false, Message = "Activity not found" });

                var activityResponse = new ActivityResponse
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
                    UpdatedAt = activity.UpdatedAt,
                    Analytics = CalculateAnalytics(activity.SensorDataPackets.ToList())
                };

                var sensorData = activity.SensorDataPackets.Select(s => new SensorDataPacketResponse
                {
                    Id = s.Id,
                    ActivityId = s.ActivityId,
                    Timestamp = s.Timestamp,
                    TimeSinceStart = s.TimeSinceStart,
                    CurrentTemperature = s.CurrentTemperature,
                    CurrentSpeed = s.CurrentSpeed,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    ElevationGain = s.ElevationGain,
                    AccelerationX = s.AccelerationX,
                    AccelerationY = s.AccelerationY,
                    AccelerationZ = s.AccelerationZ,
                    Checksum = s.Checksum,
                    IsChecksumValid = s.IsChecksumValid,
                    DeviceId = s.DeviceId,
                }).ToList();

                return Ok(new ActivityDetailsResponse
                {
                    IsSuccess = true,
                    Message = "Activity retrieved successfully",
                    Activity = activityResponse,
                    SensorData = sensorData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activity {ActivityId}", id);
                return StatusCode(500, new ActivityDetailsResponse
                {
                    IsSuccess = false,
                    Message = "Internal server error"
                });
            }
        }

        // POST: api/activities
        [HttpPost]
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
                    DeviceId = request.DeviceId,
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

        // PUT: api/activities/{id}
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<ActivityResponse>>> UpdateActivity(long id, ActivityUpdateRequest request)
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

                var activity = await _context.Activities
                    .Include(a => a.SensorDataPackets)
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

                if (activity == null)
                    return NotFound(ApiResponse<ActivityResponse>.Failure("Activity not found"));

                // Update fields if provided
                if (!string.IsNullOrEmpty(request.Name))
                    activity.Name = request.Name;
                
                if (request.Description != null)
                    activity.Description = request.Description;
                
                if (request.EndTime.HasValue)
                    activity.EndTime = request.EndTime.Value;
                
                if (request.Status.HasValue)
                    activity.Status = request.Status.Value;

                activity.UpdatedAt = DateTime.UtcNow;

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
                    DataPacketCount = activity.DataPacketCount,
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt
                };

                _logger.LogInformation("Activity {ActivityId} updated for user {UserId}", activity.Id, userId);
                return Ok(ApiResponse<ActivityResponse>.Success(response, "Activity updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating activity {ActivityId}", id);
                return StatusCode(500, ApiResponse<ActivityResponse>.Failure("Internal server error"));
            }
        }

        // DELETE: api/activities/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteActivity(long id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(ApiResponse<object>.Failure("User not found"));

                var activity = await _context.Activities
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

                if (activity == null)
                    return NotFound(ApiResponse<object>.Failure("Activity not found"));

                _context.Activities.Remove(activity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Activity {ActivityId} deleted for user {UserId}", id, userId);
                return Ok(ApiResponse<object>.Success(new object(), "Activity deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting activity {ActivityId}", id);
                return StatusCode(500, ApiResponse<object>.Failure("Internal server error"));
            }
        }

        // POST: api/activities/seed
        [HttpPost("seed")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<ActivityResponse>>> SeedActivity([FromBody] SeedActivityRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(ApiResponse<ActivityResponse>.Failure("User not found"));

                var startTime = request.StartTime?.ToUniversalTime() ?? DateTime.UtcNow;
                var interval = TimeSpan.FromSeconds(Math.Max(0.1, request.IntervalSeconds));
                var sampleCount = Math.Clamp(request.SampleCount, 10, 5000);

                // Create activity first
                var activity = new Activity
                {
                    UserId = userId.Value,
                    DeviceId = string.IsNullOrWhiteSpace(request.DeviceId) ? "test-device" : request.DeviceId,
                    Name = string.IsNullOrWhiteSpace(request.Title) ? $"Test Activity {DateTime.UtcNow:yyyy-MM-dd HH:mm}" : request.Title!,
                    Description = request.Description,
                    StartTime = startTime,
                    Status = ActivityStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Activities.Add(activity);
                await _context.SaveChangesAsync();

                // Build sensor data
                var csvPoints = request.UseTestdata
                    ? await ReadTestdataCsvPointsAsync(sampleCount)
                    : GenerateRandomCsvPoints(sampleCount);

                var packets = new List<SensorDataPacket>(csvPoints.Count);
                for (int i = 0; i < csvPoints.Count; i++)
                {
                    var p = csvPoints[i];
                    var ts = startTime.AddTicks(interval.Ticks * i);
                    packets.Add(new SensorDataPacket
                    {
                        ActivityId = activity.Id,
                        Timestamp = ts,
                        TimeSinceStart = XmlConvert.ToString(ts - startTime),
                        CurrentTemperature = p.CurrentTemperature,
                        CurrentSpeed = p.CurrentSpeed,
                        Latitude = p.Latitude,
                        Longitude = p.Longitude,
                        ElevationGain = p.ElevationGain,
                        AccelerationX = p.AccelerationX,
                        AccelerationY = p.AccelerationY,
                        AccelerationZ = p.AccelerationZ,
                        Checksum = p.Checksum,
                        IsChecksumValid = true,
                        DeviceId = activity.DeviceId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SensorDataPackets.AddRangeAsync(packets);
                activity.EndTime = packets.Count > 0 ? packets[^1].Timestamp : startTime.AddMinutes(5);
                activity.Status = ActivityStatus.Completed;
                activity.UpdatedAt = DateTime.UtcNow;
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
                    DataPacketCount = activity.DataPacketCount,
                    CreatedAt = activity.CreatedAt,
                    UpdatedAt = activity.UpdatedAt,
                    Analytics = CalculateAnalytics(packets)
                };

                return Ok(ApiResponse<ActivityResponse>.Success(response, "Seed activity created"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding activity");
                return StatusCode(500, ApiResponse<ActivityResponse>.Failure("Internal server error"));
            }
        }

        private async Task<List<CsvDataPoint>> ReadTestdataCsvPointsAsync(int max)
        {
            var result = new List<CsvDataPoint>(max);
            try
            {
                // Try typical locations for the Testdata folder
                var candidates = new[]
                {
                    Path.Combine(_env.ContentRootPath, "..", "..", "Testdata", "testdata.json"),
                    Path.Combine(_env.ContentRootPath, "Testdata", "testdata.json"),
                };

                string? path = candidates.FirstOrDefault(System.IO.File.Exists);
                if (path == null)
                {
                    _logger.LogWarning("testdata.json not found; falling back to random data");
                    return GenerateRandomCsvPoints(max);
                }

                await using var stream = System.IO.File.OpenRead(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<TestSample>(stream, options))
                {
                    if (item == null) continue;
                    result.Add(new CsvDataPoint
                    {
                        CurrentTemperature = item.current_temperature,
                        CurrentSpeed = item.current_speed,
                        Latitude = item.current_coordinates?.latitude ?? 0,
                        Longitude = item.current_coordinates?.longitude ?? 0,
                        ElevationGain = item.current_coordinates?.height ?? 0,
                        AccelerationX = item.peak_acceleration_x,
                        AccelerationY = item.peak_acceleration_y,
                        AccelerationZ = item.peak_acceleration_z,
                        Checksum = item.checksum
                    });
                    if (result.Count >= max) break;
                }

                if (result.Count == 0)
                {
                    _logger.LogWarning("No items read from testdata.json; generating random points");
                    return GenerateRandomCsvPoints(max);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed reading testdata.json; using random data");
                return GenerateRandomCsvPoints(max);
            }
        }

        private List<CsvDataPoint> GenerateRandomCsvPoints(int count)
        {
            var rnd = new Random();
            // Start at a base coordinate; small random drift
            double lat = 51.0 + rnd.NextDouble() * 0.02;
            double lon = 8.0 + rnd.NextDouble() * 0.02;
            var points = new List<CsvDataPoint>(count);
            for (int i = 0; i < count; i++)
            {
                lat += (rnd.NextDouble() - 0.5) * 0.05;
                lon += (rnd.NextDouble() - 0.5) * 0.05;
                var temp = 20 + rnd.NextDouble() * 10;
                var speed = Math.Abs(Normal(rnd, mean: 15, stddev: 5));
                var ax = (rnd.NextDouble() - 0.5) * 10;
                var ay = (rnd.NextDouble() - 0.5) * 10;
                var az = (rnd.NextDouble() - 0.5) * 10;
                var elev = 400 + (rnd.NextDouble() - 0.5) * 5;
                var checksum = temp + speed + lat + lon + elev + ax + ay + az;
                points.Add(new CsvDataPoint
                {
                    CurrentTemperature = temp,
                    CurrentSpeed = speed,
                    Latitude = lat,
                    Longitude = lon,
                    ElevationGain = elev,
                    AccelerationX = ax,
                    AccelerationY = ay,
                    AccelerationZ = az,
                    Checksum = checksum
                });
            }
            return points;
        }

        private static double Normal(Random rnd, double mean = 0, double stddev = 1)
        {
            // Box-Muller transform
            var u1 = 1.0 - rnd.NextDouble();
            var u2 = 1.0 - rnd.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stddev * randStdNormal;
        }

        private class TestSample
        {
            public double current_temperature { get; set; }
            public double current_speed { get; set; }
            public Coordinates? current_coordinates { get; set; }
            public double peak_acceleration_x { get; set; }
            public double peak_acceleration_y { get; set; }
            public double peak_acceleration_z { get; set; }
            public double checksum { get; set; }
        }

        private class Coordinates
        {
            public double latitude { get; set; }
            public double longitude { get; set; }
            public double height { get; set; }
        }

        private long? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private ActivityAnalytics CalculateAnalytics(List<SensorDataPacket> sensorData)
        {
            if (!sensorData.Any())
                return new ActivityAnalytics();

            var analytics = new ActivityAnalytics
            {
                MaxSpeed = sensorData.Max(s => s.CurrentSpeed),
                AverageSpeed = sensorData.Average(s => s.CurrentSpeed),
                AverageTemperature = sensorData.Average(s => s.CurrentTemperature),
                //MaxAcceleration = sensorData.Max(s => s.TotalAcceleration), need to change this logic 
                Route = sensorData.Select(s => new CoordinatePoint
                {
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Timestamp = s.Timestamp,
                    Speed = s.CurrentSpeed,
                    Temperature = s.CurrentTemperature
                }).ToList()
            };

            // Calculate total distance using Haversine formula
            analytics.TotalDistance = CalculateTotalDistance(analytics.Route);

            // Simple calorie calculation (can be improved with more sophisticated algorithms)
            var durationHours = sensorData.Count > 0 ? 
                (sensorData.Max(s => s.Timestamp) - sensorData.Min(s => s.Timestamp)).TotalHours : 0;
            analytics.CaloriesBurned = analytics.AverageSpeed * durationHours * 50; // Rough estimation

            // Calculate elevation gain (would need altitude data from GPS or barometric sensor)
            analytics.ElevationGain = 0; // TODO: Implement when altitude data is available

            return analytics;
        }

        private double CalculateTotalDistance(List<CoordinatePoint> route)
        {
            if (route.Count < 2) return 0;

            double totalDistance = 0;
            for (int i = 1; i < route.Count; i++)
            {
                totalDistance += CalculateDistance(
                    route[i - 1].Latitude, route[i - 1].Longitude,
                    route[i].Latitude, route[i].Longitude);
            }
            return totalDistance;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula
            const double R = 6371000; // Earth's radius in meters
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
