using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Models;

namespace Server.Models
{
    public class Activity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }
        
        [Required]
        public string DeviceId { get; set; } = String.Empty;

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public ActivityStatus Status { get; set; } = ActivityStatus.InProgress;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for sensor data
        public ICollection<SensorDataPacket> SensorDataPackets { get; set; } = new List<SensorDataPacket>();

        // Navigation property for tags
        public ICollection<ActivityTagAssignment> ActivityTagAssignments { get; set; } = new List<ActivityTagAssignment>();

        // Navigation property for summary (1:1 relationship)
        public ActivitySummary? Summary { get; set; }

        // Computed properties for analytics
        [NotMapped]
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        [NotMapped]
        public bool IsActive => Status == ActivityStatus.InProgress;

        [NotMapped]
        public int DataPacketCount => SensorDataPackets?.Count ?? 0;
    }
}
