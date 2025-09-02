using System.Text.Json;
using System.Net.Http.Json;
using Shared.Models;

namespace Frontend.Client.Services;

public interface IRoutePlannerService
{
    Task<RouteResponse> CalculateRouteAsync(RouteRequest request);
    Task<AddressSearchResponse> SearchAddressAsync(AddressSearchRequest request);
}

public class RoutePlannerService : IRoutePlannerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoutePlannerService> _logger;

    public RoutePlannerService(HttpClient httpClient, ILogger<RoutePlannerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RouteResponse> CalculateRouteAsync(RouteRequest request)
    {
        try
        {
            _logger.LogInformation("Calculating route with {WaypointCount} waypoints", request.Waypoints.Count);

            var response = await _httpClient.PostAsJsonAsync("/api/routes/calculate", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RouteResponse>();
                
                if (result?.Success == true)
                {
                    _logger.LogInformation("Route calculated: {Distance}m in {Duration}s", 
                        result.Route?.Distance, result.Route?.Duration);
                }
                
                return result ?? new RouteResponse { Success = false, Error = "Invalid response" };
            }
            else
            {
                _logger.LogError("Route calculation failed: {StatusCode}", response.StatusCode);
                return new RouteResponse 
                { 
                    Success = false, 
                    Error = $"Server error: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating route");
            return new RouteResponse 
            { 
                Success = false, 
                Error = "Network error occurred" 
            };
        }
    }

    public async Task<AddressSearchResponse> SearchAddressAsync(AddressSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Searching for address: {Query}", request.Query);

            var response = await _httpClient.PostAsJsonAsync("/api/routes/search-address", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AddressSearchResponse>();
                
                _logger.LogInformation("Found {Count} addresses", result?.Results.Count ?? 0);
                
                return result ?? new AddressSearchResponse { Success = false, Error = "Invalid response" };
            }
            else
            {
                _logger.LogError("Address search failed: {StatusCode}", response.StatusCode);
                return new AddressSearchResponse 
                { 
                    Success = false, 
                    Error = $"Server error: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching address");
            return new AddressSearchResponse 
            { 
                Success = false, 
                Error = "Network error occurred" 
            };
        }
    }
}
