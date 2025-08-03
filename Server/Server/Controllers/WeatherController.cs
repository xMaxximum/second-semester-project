using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Server.Services;
using Microsoft.AspNetCore.Authorization;

namespace Server.Controllers{
[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weatherService;

    public WeatherController(WeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<WeatherData>> Get()
    {
        try
        {
            var data = await _weatherService.GetWeatherAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
}
