using System.Text.Json.Serialization;

namespace Shared.Models
{
    public enum WeatherCondition
    {
        Sunny,
        Cloudy,
        Rainy,
        PartlyCloudy
    }

    public class WeatherData
    {
        public string City { get; set; }
        public string CountryCode { get; set; }
        public string Datetime { get; set; }
        public int Temperature { get; set; }
        
        [JsonPropertyName(("feels_like"))]
        public int FeelsLike { get; set; }
        public string Description { get; set; }
        public string WindDirection { get; set; }
        public int Humidity { get; set; }
        public int CloudCoverage { get; set; }
        public int WindSpeed { get; set; }
        public WeatherCondition Condition { get; set; }
        public long timestamp { get; set; }
    }
    
    public class LocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string City { get; set; } = string.Empty;
    }
}
