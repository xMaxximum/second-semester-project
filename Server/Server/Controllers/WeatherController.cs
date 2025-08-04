using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Server.Services;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;

        public WeatherController(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        public class LocationRequest
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string City { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<ActionResult<WeatherData>> GetWeather([FromBody] LocationRequest location)
        {
            try
            {
                var data = await _weatherService.GetWeatherAsync(location.Lat, location.Lon, location.City);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
