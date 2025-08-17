using Microsoft.Extensions.Options;
using Shared.Models;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Server.Services
{
    public class WeatherService
    {
        private Dictionary<string, WeatherData> cachedWeatherData = new Dictionary<string, WeatherData>();
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public WeatherService(HttpClient http, IOptions<ApiOptions> options)
        {
            _http = http;
            _apiKey = options.Value.WeatherApiKey;
        }

        // Method for getting weather by coordinates (if user provides exact lat/lon)
        public async Task<WeatherData> GetWeatherAsync(double lat, double lon)
        {
            var cacheKey = $"{lat:F2},{lon:F2}";
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Method for getting weather by city name (most common use case)
        public async Task<WeatherData> GetWeatherByCityAsync(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                throw new ArgumentException("City name cannot be empty", nameof(cityName));

            var cacheKey = cityName.ToLowerInvariant();
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cityName)}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed - city not found");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Method for getting weather by city name and country code for better accuracy
        public async Task<WeatherData> GetWeatherByCityAndCountryAsync(string cityName, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                throw new ArgumentException("City name cannot be empty", nameof(cityName));
            
            if (string.IsNullOrWhiteSpace(countryCode))
                throw new ArgumentException("Country code cannot be empty", nameof(countryCode));

            var cacheKey = $"{cityName.ToLowerInvariant()},{countryCode.ToUpperInvariant()}";
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cityName)},{countryCode.ToUpperInvariant()}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed - city/country combination not found");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Helper method to create WeatherData from API response
        private WeatherData CreateWeatherData(OpenWeatherResponse weather)
        {
            return new WeatherData
            {
                City = weather.Name,
                CountryCode = weather.Sys.Country,
                Datetime = DateTime.Now.ToString("dddd, d MMMM yyyy HH:mm", new CultureInfo("en-US")),
                Temperature = (int)Math.Round(weather.Main.Temp),
                FeelsLike = (int)Math.Round(weather.Main.FeelsLike),
                Description = weather.Weather[0].Description,
                WindDirection = ConvertWindDirection(weather.Wind.Deg),
                Humidity = weather.Main.Humidity,
                CloudCoverage = weather.Clouds.All,
                WindSpeed = (int)Math.Round(weather.Wind.Speed),
                Condition = MapCondition(weather.Weather[0].Main),
                timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond
            };
        }

        private string ConvertWindDirection(int deg)
        {
            if (deg >= 315 || deg < 45) return "N";
            if (deg >= 45 && deg < 135) return "E";
            if (deg >= 135 && deg < 225) return "S";
            return "W";
        }

        private WeatherCondition MapCondition(string main) => main.ToLower() switch
        {
            "clear" => WeatherCondition.Sunny,
            "clouds" => WeatherCondition.Cloudy,
            "rain" => WeatherCondition.Rainy,
            "drizzle" => WeatherCondition.Rainy,
            _ => WeatherCondition.PartlyCloudy
        };

        private class OpenWeatherResponse
        {
            public WeatherMain Main { get; set; } = null!;
            public List<WeatherDescription> Weather { get; set; } = null!;
            public WindInfo Wind { get; set; } = null!;
            public CloudsInfo Clouds { get; set; } = null!;
            public SysInfo Sys { get; set; } = null!;
            public string Name { get; set; } = string.Empty;

            public class WeatherMain
            {
                public double Temp { get; set; }            
                
                [JsonPropertyName("feels_like")]
                public double FeelsLike { get; set; }
                public int Humidity { get; set; }
            }

            public class WeatherDescription
            {
                public string Main { get; set; } = string.Empty;
                public string Description { get; set; } = string.Empty;
            }

            public class WindInfo
            {
                public double Speed { get; set; }
                public int Deg { get; set; }
            }

            public class CloudsInfo
            {
                public int All { get; set; }
            }
        }

        private class SysInfo
        {
            public string Country { get; set; } = string.Empty;
        }
    }
}
