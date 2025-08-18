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
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? City { get; set; }
        public string? CountryCode { get; set; }
    }

    // Geocoding models for autocomplete functionality
    public class GeocodingResult
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> LocalNames { get; set; } = new();
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Country { get; set; } = string.Empty;
        public string? State { get; set; }
    }

    public class LocationSuggestion
    {
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? State { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        // Display text for the autocomplete
        public string DisplayText => State != null ? $"{Name}, {State}, {Country}" : $"{Name}, {Country}";
        
        // Search text for filtering
        public string SearchText => $"{Name} {State} {Country}".Trim();
    }

    // ISO 3166 country codes for autocomplete
    public class CountryCode
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        public string DisplayText => $"{Code} - {Name}";
    }
}
