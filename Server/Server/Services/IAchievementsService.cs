
namespace Server.Services
{
    public interface IAchievementsService
    {
        Task<Dictionary<string, DateTime>> GetCurrentWeekAsync(long userId, CancellationToken ct = default);
        Task<bool> GrantAsync(long userId, string achievementId, CancellationToken ct = default);
    }
}
