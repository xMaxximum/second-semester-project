using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;


namespace Server.Models;


[Index(nameof(UserId), nameof(AchievementId), nameof(WeekIsoYear), nameof(WeekIsoNumber), IsUnique = true)]
public class Achievement
{
    [Key] public long Id { get; set; }
    public long UserId { get; set; }
    [Required, MaxLength(64)] public string AchievementId { get; set; } = default!;
    public DateTime EarnedAtUtc { get; set; } = DateTime.UtcNow;


    public int WeekIsoYear { get; set; }
    public int WeekIsoNumber { get; set; }
}
