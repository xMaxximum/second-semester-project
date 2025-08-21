using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class Device
    {
        [Key]
        [StringLength(50)]
        public string DeviceId { get; set; } = string.Empty; // ESP32 MAC address or unique ID (Primary Key)

        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // User-friendly name like "My ESP32 Bike Tracker"

        [Required]
        public long UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [StringLength(50)]
        public string? FirmwareVersion { get; set; } = "1.0";

        [StringLength(128)]
        public string? AuthToken { get; set; } // Permanent authentication token for device API calls

        [StringLength(10)]
        public string? ActivationCode { get; set; } // Temporary activation code (6-8 digits)

        public DateTime? ActivationCodeExpiry { get; set; } // When the activation code expires

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

    }
}
