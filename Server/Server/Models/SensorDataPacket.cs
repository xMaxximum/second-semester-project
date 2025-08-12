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
        
        [Required]
        public double ElevationGain { get; set; }

        // Acceleration data
        [Required]
        public double AccelerationX { get; set; }   

        [Required]
        public double AccelerationY { get; set; }

        [Required]
        public double AccelerationZ { get; set; }

        // Data integrity
        [Required]
        public double Checksum { get; set; }

        [Required]
        public bool IsChecksumValid { get; set; } = true;

        // Additional metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? DeviceId { get; set; } // ESP32 device identifier (MAC address or unique string)
        
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
            var calculatedChecksum = CurrentTemperature + CurrentSpeed + Latitude + Longitude + ElevationGain +
                                   AccelerationX + AccelerationY + AccelerationZ;
            
            return Math.Abs(calculatedChecksum - Checksum) < 0.001; // Allow for small floating point differences
        }
    }
}
