using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Models;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.DefaultRoute + "/activities")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityController> _logger;

        public ActivityController(ApplicationDbContext context, ILogger<ActivityController> logger)
        {
            _context = context;
            _logger = logger;
        }

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
                
                var activities = await query
                    .Include(a => a.SensorDataPackets)
                    .OrderByDescending(a => a.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new ActivityResponse
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
                        UpdatedAt = a.UpdatedAt
                    })
                    .ToListAsync();

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
