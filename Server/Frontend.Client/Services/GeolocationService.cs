using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend.Client.Services;

public class GeolocationService
{
    private readonly IJSRuntime _jsRuntime;

    public GeolocationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<GeolocationResult> GetCurrentPositionAsync()
    {
        try
        {
            Console.WriteLine("Requesting current position...");
            var positionJson = await _jsRuntime.InvokeAsync<string>("blazorGeolocation.getCurrentPosition");
            Console.WriteLine($"Received position JSON: {positionJson}");

            var position = JsonSerializer.Deserialize<GeolocationPosition>(positionJson);
            Console.WriteLine($"Deserialized position - Lat: {position?.Latitude}, Lng: {position?.Longitude}");

            if (position?.Latitude == null || position?.Longitude == null)
            {
                return GeolocationResult.Error("Could not retrieve valid coordinates from your location.");
            }

            // Validate coordinate ranges
            if (Math.Abs(position.Latitude.Value) > 90 || Math.Abs(position.Longitude.Value) > 180)
            {
                return GeolocationResult.Error("Invalid coordinates received. Please try manual entry.");
            }

            return GeolocationResult.Success(position.Latitude.Value, position.Longitude.Value, position.Accuracy);
        }
        catch (JSException jsEx)
        {
            Console.WriteLine($"JavaScript geolocation error: {jsEx.Message}");
            return GeolocationResult.Error(jsEx.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get current location: {ex.Message}");
            return GeolocationResult.Error("Failed to get your current location. Please try entering your location manually.");
        }
    }

    private class GeolocationPosition
    {
        [JsonPropertyName("latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("longitude")] public double? Longitude { get; set; }
        [JsonPropertyName("accuracy")] public double? Accuracy { get; set; }
    }
}

public class GeolocationResult
{
    public bool IsSuccess { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? Accuracy { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    private GeolocationResult() { }

    public static GeolocationResult Success(double latitude, double longitude, double? accuracy = null)
    {
        return new GeolocationResult
        {
            IsSuccess = true,
            Latitude = latitude,
            Longitude = longitude,
            Accuracy = accuracy
        };
    }

    public static GeolocationResult Error(string errorMessage)
    {
        return new GeolocationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
