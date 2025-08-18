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

        [HttpPost]
        public async Task<ActionResult<WeatherData>> GetWeather([FromBody] LocationRequest location)
        {
            try
            {
                WeatherData data;

                // Priority order: coordinates > city+country > city only
                if (location.Latitude.HasValue && location.Longitude.HasValue)
                {
                    // Use coordinates if provided (most accurate)
                    data = await _weatherService.GetWeatherAsync(location.Latitude.Value, location.Longitude.Value);
                }
                else if (!string.IsNullOrWhiteSpace(location.City) && !string.IsNullOrWhiteSpace(location.CountryCode))
                {
                    // Use city + country code (good accuracy)
                    data = await _weatherService.GetWeatherByCityAndCountryAsync(location.City, location.CountryCode);
                }
                else if (!string.IsNullOrWhiteSpace(location.City))
                {
                    // Use city name only (may be ambiguous for common city names)
                    data = await _weatherService.GetWeatherByCityAsync(location.City);
                }
                else
                {
                    return BadRequest("Please provide either coordinates (latitude/longitude) or a city name.");
                }

                return Ok(data);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching weather data: {ex.Message}");
            }
        }

        [HttpGet("by-city/{cityName}")]
        public async Task<ActionResult<WeatherData>> GetWeatherByCity(string cityName)
        {
            try
            {
                var data = await _weatherService.GetWeatherByCityAsync(cityName);
                return Ok(data);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching weather data: {ex.Message}");
            }
        }

        [HttpGet("by-city/{cityName}/country/{countryCode}")]
        public async Task<ActionResult<WeatherData>> GetWeatherByCityAndCountry(string cityName, string countryCode)
        {
            try
            {
                var data = await _weatherService.GetWeatherByCityAndCountryAsync(cityName, countryCode);
                return Ok(data);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching weather data: {ex.Message}");
            }
        }

        [HttpGet("by-coordinates")]
        public async Task<ActionResult<WeatherData>> GetWeatherByCoordinates([FromQuery] double latitude, [FromQuery] double longitude)
        {
            try
            {
                var data = await _weatherService.GetWeatherAsync(latitude, longitude);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching weather data: {ex.Message}");
            }
        }

        [HttpGet("search-locations")]
        public async Task<ActionResult<List<LocationSuggestion>>> SearchLocations([FromQuery] string query, [FromQuery] int limit = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new List<LocationSuggestion>());
                }

                var results = await _weatherService.SearchLocationsAsync(query, limit);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while searching locations: {ex.Message}");
            }
        }

        [HttpGet("country-codes")]
        public ActionResult<List<CountryCode>> GetCountryCodes()
        {
            try
            {
                var countryCodes = WeatherService.GetCountryCodes();
                return Ok(countryCodes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching country codes: {ex.Message}");
            }
        }
    }
}
