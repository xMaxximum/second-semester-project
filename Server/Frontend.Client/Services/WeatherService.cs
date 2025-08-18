using System.Text.Json;
using System.Net.Http.Json;
using Shared.Models;

namespace Frontend.Client.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherData?> GetWeatherAsync(LocationRequest locationRequest)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/weather", locationRequest);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WeatherData>();
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Weather API error: {errorContent}");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to fetch weather data: {ex.Message}");
            return null;
        }
    }

    public async Task<List<LocationSuggestion>> SearchLocationsAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<LocationSuggestion>();

        try
        {
            var response = await _httpClient.GetAsync($"api/weather/search-locations?query={Uri.EscapeDataString(query)}&limit={limit}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var results = await response.Content.ReadFromJsonAsync<List<LocationSuggestion>>(cancellationToken: cancellationToken);
                return results ?? new List<LocationSuggestion>();
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled, return empty list
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error searching cities: {ex.Message}");
        }

        return new List<LocationSuggestion>();
    }

    public async Task<List<CountryCode>> GetCountryCodesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/weather/country-codes");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<CountryCode>>() ?? new List<CountryCode>();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load country codes: {ex.Message}");
        }

        return new List<CountryCode>();
    }
}
