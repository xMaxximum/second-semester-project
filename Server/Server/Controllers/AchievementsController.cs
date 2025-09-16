using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/activities/achievements")]
[Authorize]
public class AchievementsController : ControllerBase
{
    private readonly IAchievementsService _svc;

    public AchievementsController(IAchievementsService svc)
    {
        _svc = svc;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    // GET: /api/activities/achievements/current
    [HttpGet("current")]
    public async Task<ActionResult<Dictionary<string, DateTime>>> GetCurrent(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var map = await _svc.GetCurrentWeekAsync(userId, ct);
        return Ok(map); // { "d100": "2025-09-14T20:00:00Z", ... }
    }

    // POST: /api/activities/achievements/grant/{achievementId}
    [HttpPost("grant/{achievementId}")]
    public async Task<IActionResult> Grant(string achievementId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var ok = await _svc.GrantAsync(userId, achievementId, ct);
        return ok ? Ok() : Conflict();
    }
}
