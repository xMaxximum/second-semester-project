using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    // Activity Models
    public class ActivityCreateRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime? StartTime { get; set; } // If null, uses current time
    }

    public class ActivityUpdateRequest
    {
        [Required]
        public long Id { get; set; }

        [StringLength(100, MinimumLength = 1)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime? EndTime { get; set; }

        public ActivityStatus? Status { get; set; }
    }

    public class ActivityResponse
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public ActivityStatus Status { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsActive { get; set; }
        public int DataPacketCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Analytics data
        public ActivityAnalytics? Analytics { get; set; }
    }

    public class ActivityAnalytics
    {
        public double TotalDistance { get; set; } // in meters
        public double MaxSpeed { get; set; }
        public double AverageSpeed { get; set; }
        public double ElevationGain { get; set; }
        public double CaloriesBurned { get; set; }
        public double AverageTemperature { get; set; }
        public double MaxAcceleration { get; set; }
        public List<CoordinatePoint> Route { get; set; } = new();
    }

    public class CoordinatePoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public double Speed { get; set; }
        public double Temperature { get; set; }
    }

    // Sensor Data Models
    public class SensorDataPacketRequest
    {
        [Required]
        public long ActivityId { get; set; }

        [Required]
        [StringLength(50)]
        public string TimeSinceStart { get; set; } = string.Empty;

        [Required]
        [Range(-50.0, 100.0)]
        public double CurrentTemperature { get; set; }

        [Required]
        [Range(0.0, 200.0)]
        public double CurrentSpeed { get; set; }

        [Required]
        [Range(-90.0, 90.0)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180.0, 180.0)]
        public double Longitude { get; set; }

        [Required]
        public double AveragedAccelerationX { get; set; }

        [Required]
        public double AveragedAccelerationY { get; set; }

        [Required]
        public double AveragedAccelerationZ { get; set; }

        [Required]
        public double PeakAccelerationX { get; set; }

        [Required]
        public double PeakAccelerationY { get; set; }

        [Required]
        public double PeakAccelerationZ { get; set; }

        [Required]
        public double Checksum { get; set; }

        [StringLength(50)]
        public string? DeviceId { get; set; }
    }

    public class SensorDataPacketResponse
    {
        public long Id { get; set; }
        public long ActivityId { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimeSinceStart { get; set; } = string.Empty;
        public double CurrentTemperature { get; set; }
        public double CurrentSpeed { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AveragedAccelerationX { get; set; }
        public double AveragedAccelerationY { get; set; }
        public double AveragedAccelerationZ { get; set; }
        public double PeakAccelerationX { get; set; }
        public double PeakAccelerationY { get; set; }
        public double PeakAccelerationZ { get; set; }
        public double Checksum { get; set; }
        public bool IsChecksumValid { get; set; }
        public string? DeviceId { get; set; }
        public double TotalAcceleration { get; set; }
        public double TotalPeakAcceleration { get; set; }
    }

    // Common Response Models
    public class ActivityListResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ActivityResponse> Activities { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class ActivityDetailsResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public ActivityResponse? Activity { get; set; }
        public List<SensorDataPacketResponse> SensorData { get; set; } = new();
    }

    public class SensorDataResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<SensorDataPacketResponse> SensorData { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public ApiResponse(bool isSuccess, string message = "", T? data = default)
        {
            IsSuccess = isSuccess;
            Message = message;
            Data = data;
        }

        public static ApiResponse<T> Success(T data, string message = "Operation successful")
            => new(true, message, data);

        public static ApiResponse<T> Failure(string message, List<string>? errors = null)
            => new(false, message) { Errors = errors ?? new List<string>() };
    }

    public enum ActivityStatus
    {
        InProgress = 0,
        Completed = 1,
        Paused = 2,
        Cancelled = 3
    }
}
