using System.Text.Json;
using Shared.Models;

namespace Frontend.Client.Services;

public class WeatherLocationService
{
    private readonly ICookie _cookie;
    private const string LocationCookieKey = "weather_location";

    public WeatherLocationService(ICookie cookie)
    {
        _cookie = cookie;
    }

    public async Task<LocationRequest?> GetSavedLocationAsync()
    {
        try
        {
            var savedLocationJson = await _cookie.GetValue(LocationCookieKey);
            if (!string.IsNullOrEmpty(savedLocationJson))
            {
                return JsonSerializer.Deserialize<LocationRequest>(savedLocationJson);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load location from cookie: {ex.Message}");
        }
        
        return null;
    }

    public async Task SaveLocationAsync(LocationRequest location)
    {
        try
        {
            var locationJson = JsonSerializer.Serialize(location);
            await _cookie.SetValue(LocationCookieKey, locationJson, 365); // Save for 1 year
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save location to cookie: {ex.Message}");
        }
    }

    public string GetLocationDisplayText(LocationRequest location)
    {
        if (location == null)
            return "Unknown Location";
            
        if (!string.IsNullOrWhiteSpace(location.City))
        {
            return string.IsNullOrWhiteSpace(location.CountryCode)
                ? location.City
                : $"{location.City}, {location.CountryCode}";
        }
        
        if (location.Latitude.HasValue && location.Longitude.HasValue)
        {
            return $"{location.Latitude:F4}, {location.Longitude:F4}";
        }
        
        return "Unknown Location";
    }

    public LocationRequest EnhanceLocationWithWeatherData(LocationRequest originalLocation, WeatherData weatherData)
    {
        if (originalLocation == null || weatherData == null)
            return originalLocation ?? new LocationRequest();

        return new LocationRequest
        {
            Latitude = originalLocation.Latitude,
            Longitude = originalLocation.Longitude,
            City = !string.IsNullOrWhiteSpace(weatherData.City) ? weatherData.City : originalLocation.City,
            CountryCode = !string.IsNullOrWhiteSpace(weatherData.CountryCode) ? weatherData.CountryCode : originalLocation.CountryCode
        };
    }
}
