
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;

namespace Server.Services
{
    public sealed class AchievementsService : IAchievementsService
    {
        private readonly ApplicationDbContext _db;
        private static readonly TimeZoneInfo BerlinTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        public AchievementsService(ApplicationDbContext db)
        {
            _db = db;
        }

        private static (int year, int week) CurrentIsoWeekBerlin()
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BerlinTz);
            return (ISOWeek.GetYear(local), ISOWeek.GetWeekOfYear(local));
        }

        public async Task<Dictionary<string, DateTime>> GetCurrentWeekAsync(long userId, CancellationToken ct = default)
        {
            var (y, w) = CurrentIsoWeekBerlin();

            var rows = await _db.Achievements
                .Where(g => g.UserId == userId && g.WeekIsoYear == y && g.WeekIsoNumber == w)
                .ToListAsync(ct);

            return rows.ToDictionary(g => g.AchievementId, g => g.EarnedAtUtc);
        }

        public async Task<bool> GrantAsync(long userId, string achievementId, CancellationToken ct = default)
        {
            var (y, w) = CurrentIsoWeekBerlin();

            var exists = await _db.Achievements.AnyAsync(g =>
                g.UserId == userId &&
                g.AchievementId == achievementId &&
                g.WeekIsoYear == y &&
                g.WeekIsoNumber == w, ct);

            if (exists) return false;

            _db.Achievements.Add(new Achievement
            {
                UserId = userId,
                AchievementId = achievementId,
                WeekIsoYear = y,
                WeekIsoNumber = w,
                EarnedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
