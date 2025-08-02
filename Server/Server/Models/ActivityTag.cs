using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class ActivityTag
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(7)] // For hex color codes like #FF5733
        public string? Color { get; set; }

        [Required]
        public long UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<ActivityTagAssignment> ActivityTagAssignments { get; set; } = new List<ActivityTagAssignment>();
    }

    public class ActivityTagAssignment
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long ActivityId { get; set; }

        [ForeignKey(nameof(ActivityId))]
        public Activity Activity { get; set; } = null!;

        [Required]
        public long TagId { get; set; }

        [ForeignKey(nameof(TagId))]
        public ActivityTag Tag { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
