using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class ActivitySummary
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long ActivityId { get; set; }

        [ForeignKey(nameof(ActivityId))]
        public Activity Activity { get; set; } = null!;

        // Distance and Movement
        [Range(0.0, double.MaxValue)]
        public double TotalDistanceMeters { get; set; }

        [Range(0.0, 500.0)] // Max speed in km/h
        public double MaxSpeedKmh { get; set; }

        [Range(0.0, 500.0)]
        public double AverageSpeedKmh { get; set; }

        // Environmental
        [Range(-50.0, 100.0)]
        public double AverageTemperatureCelsius { get; set; }

        [Range(-50.0, 100.0)]
        public double MinTemperatureCelsius { get; set; }

        [Range(-50.0, 100.0)]
        public double MaxTemperatureCelsius { get; set; }

        // Elevation (future enhancement when altitude data is available)
        [Range(-1000.0, 10000.0)] // Meters above sea level
        public double? ElevationGainMeters { get; set; }

        [Range(-1000.0, 10000.0)]
        public double? MaxElevationMeters { get; set; }

        [Range(-1000.0, 10000.0)]
        public double? MinElevationMeters { get; set; }

        // Acceleration and Movement Quality
        public double MaxAccelerationMs2 { get; set; }
        public double AverageAccelerationMs2 { get; set; }

        // Health & Fitness Estimates
        [Range(0.0, 10000.0)]
        public double EstimatedCaloriesBurned { get; set; }

        // Data Quality
        public int TotalDataPackets { get; set; }
        public int ValidDataPackets { get; set; }
        public double DataQualityPercentage { get; set; }

        // Route Information
        public double? StartLatitude { get; set; }
        public double? StartLongitude { get; set; }
        public double? EndLatitude { get; set; }
        public double? EndLongitude { get; set; }

        // Timing
        public TimeSpan ActiveDuration { get; set; } // Time spent actually moving
        public TimeSpan TotalDuration { get; set; } // Total time from start to end

        // Metadata
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
        public bool IsStale { get; set; } = false; // Set to true when activity is updated

        // Method to calculate calories (you can implement more sophisticated algorithms)
        public void CalculateEstimatedCalories(double userWeightKg = 70.0)
        {
            // Simple MET-based calculation (can be enhanced)
            // For cycling: moderate = 8 METs, vigorous = 12 METs
            var durationHours = TotalDuration.TotalHours;
            var metValue = AverageSpeedKmh < 15 ? 6.0 : AverageSpeedKmh < 25 ? 8.0 : 12.0;
            EstimatedCaloriesBurned = metValue * userWeightKg * durationHours;
        }
    }
}
