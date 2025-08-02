using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class Device
    {
        [Key]
        [StringLength(50)]
        public string DeviceId { get; set; } = string.Empty; // ESP32 MAC address or unique ID (Primary Key)

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // User-friendly name like "My ESP32 Bike Tracker"

        [Required]
        public long UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [StringLength(50)]
        public string? HardwareVersion { get; set; }

        [StringLength(50)]
        public string? FirmwareVersion { get; set; }

        [StringLength(50)]
        public string? DeviceType { get; set; } = "ESP32"; // Future: could support different device types

        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Device status
        public double? LastKnownBatteryLevel { get; set; } // 0-100%
        public string? LastKnownLocation { get; set; } // JSON with lat/lng
        public double? LastKnownTemperature { get; set; }

        // Device settings (JSON)
        [StringLength(1000)]
        public string? Settings { get; set; } // JSON for device-specific settings

        // Navigation properties
        public ICollection<SensorDataPacket> SensorDataPackets { get; set; } = new List<SensorDataPacket>();
    }
}
