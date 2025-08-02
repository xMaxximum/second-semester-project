using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class SensorDataPacket
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long ActivityId { get; set; }

        [ForeignKey(nameof(ActivityId))]
        public Activity Activity { get; set; } = null!;

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Time since activity start (from ESP32 data)
        [Required]
        [StringLength(50)]
        public string TimeSinceStart { get; set; } = string.Empty; // ISO 8601 duration format (e.g., "PT1H30M")

        // Environmental data
        [Required]
        [Range(-50.0, 100.0)]
        public double CurrentTemperature { get; set; }

        // Movement data
        [Required]
        [Range(0.0, 200.0)] // Speed in km/h or m/s
        public double CurrentSpeed { get; set; }

        // GPS coordinates
        [Required]
        [Range(-90.0, 90.0)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180.0, 180.0)]
        public double Longitude { get; set; }

        // Acceleration data (averaged)
        [Required]
        public double AveragedAccelerationX { get; set; }

        [Required]
        public double AveragedAccelerationY { get; set; }

        [Required]
        public double AveragedAccelerationZ { get; set; }

        // Acceleration data (peak values)
        [Required]
        public double PeakAccelerationX { get; set; }

        [Required]
        public double PeakAccelerationY { get; set; }

        [Required]
        public double PeakAccelerationZ { get; set; }

        // Data integrity
        [Required]
        public double Checksum { get; set; }

        [Required]
        public bool IsChecksumValid { get; set; } = true;

        // Additional metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? DeviceId { get; set; } // ESP32 device identifier (MAC address or unique string)

        // Navigation property to Device (optional)
        [ForeignKey(nameof(DeviceId))]
        public Device? Device { get; set; }

        // Computed properties for analytics
        [NotMapped]
        public double TotalAcceleration => Math.Sqrt(
            Math.Pow(AveragedAccelerationX, 2) + 
            Math.Pow(AveragedAccelerationY, 2) + 
            Math.Pow(AveragedAccelerationZ, 2));

        [NotMapped]
        public double TotalPeakAcceleration => Math.Sqrt(
            Math.Pow(PeakAccelerationX, 2) + 
            Math.Pow(PeakAccelerationY, 2) + 
            Math.Pow(PeakAccelerationZ, 2));

        [NotMapped]
        public TimeSpan TimeSinceStartParsed
        {
            get
            {
                try
                {
                    return System.Xml.XmlConvert.ToTimeSpan(TimeSinceStart);
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        // Method to validate checksum
        public bool ValidateChecksum()
        {
            var calculatedChecksum = CurrentTemperature + CurrentSpeed + Latitude + Longitude +
                                   AveragedAccelerationX + AveragedAccelerationY + AveragedAccelerationZ +
                                   PeakAccelerationX + PeakAccelerationY + PeakAccelerationZ;
            
            return Math.Abs(calculatedChecksum - Checksum) < 0.001; // Allow for small floating point differences
        }
    }
}
