using System.Text.Json;
using Shared.Models;

namespace Server.Services;

public interface IRouteService
{
    Task<RouteResponse> CalculateRouteAsync(RouteRequest request);
    Task<AddressSearchResponse> SearchAddressAsync(AddressSearchRequest request);
    Task<List<SavedRoute>> GetSavedRoutesAsync(long userId);
    Task<SavedRoute?> SaveRouteAsync(SaveRouteRequest request, long userId);
    Task<bool> DeleteSavedRouteAsync(int routeId, long userId);
}

public class RouteService : IRouteService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RouteService> _logger;

    // Using OpenRouteService as the default provider
    // You can also use OSRM, GraphHopper, or other routing services
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public RouteService(HttpClient httpClient, IConfiguration configuration, ILogger<RouteService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Configure routing service - you can change this to other providers
        _baseUrl = configuration["Routing:OpenRouteService:BaseUrl"] ?? "https://api.openrouteservice.org";
        _apiKey = configuration["Routing:OpenRouteService:ApiKey"] ?? "";

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OpenRouteService API key not configured. Using free tier with limitations.");
        }
    }

    public async Task<RouteResponse> CalculateRouteAsync(RouteRequest request)
    {
        try
        {
            if (request.Waypoints.Count < 2)
            {
                return new RouteResponse
                {
                    Success = false,
                    Error = "At least 2 waypoints are required"
                };
            }

            // Convert waypoints to OpenRouteService format: [lon, lat]
            // Round to 6 decimals to avoid FP noise while keeping ~0.1m precision
            var coordinates = request.Waypoints
                .Select(w => new double[] { Math.Round(w.Longitude, 6), Math.Round(w.Latitude, 6) })
                .ToArray();

            var profile = GetProfileString(request.Profile);
            var url = $"{_baseUrl}/v2/directions/{profile}/geojson";

            var payload = new Dictionary<string, object?>
            {
                ["coordinates"] = coordinates,
                ["instructions"] = true,
                ["elevation"] = true,
                ["extra_info"] = new[] { "surface", "steepness" },
                ["options"] = new Dictionary<string, object?>
                {
                    ["avoid_features"] = GetAvoidFeatures(request)
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = content;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", _apiKey);
            }
            
            // Debug log to verify outgoing coordinates order/values
            try
            {
                var coordsPreview = string.Join(" | ", coordinates.Select(c => $"[{c[0]},{c[1]}]"));
                _logger.LogInformation("ORS request profile={Profile} coords={Coords}", profile, coordsPreview);
            }
            catch { /* logging should not break routing */ }

            var response = await _httpClient.SendAsync(requestMessage);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Routing API error: {response.StatusCode} - {responseJson}");
                return new RouteResponse 
                { 
                    Success = false, 
                    Error = $"Routing service error: {response.StatusCode}" 
                };
            }

            var routeData = ParseOpenRouteServiceResponse(responseJson);

            return new RouteResponse
            {
                Success = true,
                Route = routeData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating route");
            return new RouteResponse
            {
                Success = false,
                Error = "An error occurred while calculating the route"
            };
        }
    }

    public async Task<AddressSearchResponse> SearchAddressAsync(AddressSearchRequest request)
    {
        try
        {
            var url = $"{_baseUrl}/geocode/search?text={Uri.EscapeDataString(request.Query)}&size={request.Limit}";
            
            // Add API key as query parameter
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url += $"&api_key={_apiKey}";
            }
            
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                url += $"&focus.point.lat={request.Latitude}&focus.point.lon={request.Longitude}";
            }

            if (!string.IsNullOrEmpty(request.CountryCode))
            {
                url += $"&boundary.country={request.CountryCode}";
            }

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return new AddressSearchResponse
                {
                    Success = false,
                    Error = $"Geocoding service error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var results = ParseGeocodingResponse(responseJson);

            return new AddressSearchResponse
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching addresses");
            return new AddressSearchResponse
            {
                Success = false,
                Error = "An error occurred while searching addresses"
            };
        }
    }

    public async Task<List<SavedRoute>> GetSavedRoutesAsync(long userId)
    {
        // TODO: Implement database storage for saved routes
        // For now, return empty list
        await Task.CompletedTask;
        return new List<SavedRoute>();
    }

    public async Task<SavedRoute?> SaveRouteAsync(SaveRouteRequest request, long userId)
    {
        // TODO: Implement database storage for saved routes
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> DeleteSavedRouteAsync(int routeId, long userId)
    {
        // TODO: Implement database storage for saved routes
        await Task.CompletedTask;
        return false;
    }

    private string GetProfileString(RouteProfile profile)
    {
        return profile switch
        {
            RouteProfile.Driving => "driving-car",
            RouteProfile.Cycling => "cycling-regular",
            RouteProfile.CyclingMountain => "cycling-mountain",
            RouteProfile.CyclingRoad => "cycling-road",
            RouteProfile.CyclingElectric => "cycling-electric",
            RouteProfile.Walking => "foot-walking",
            RouteProfile.Wheelchair => "wheelchair",
            _ => "cycling-regular"
        };
    }

    private string[] GetAvoidFeatures(RouteRequest request)
    {
        var avoid = new List<string>();
        
        if (request.AvoidTolls) avoid.Add("tollways");
        if (request.AvoidHighways) avoid.Add("highways");
        if (request.AvoidFerries) avoid.Add("ferries");
        
        return avoid.ToArray();
    }

    private Shared.Models.RouteData ParseOpenRouteServiceResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        
        // GeoJSON format has a 'features' array
        var features = root.GetProperty("features");
        if (features.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No routes found in response");
        }

        var routeFeature = features[0];
        var properties = routeFeature.GetProperty("properties");
        var summary = properties.GetProperty("summary");
        var segments = properties.GetProperty("segments");
        var geometry = routeFeature.GetProperty("geometry");

        var routeData = new Shared.Models.RouteData
        {
            Distance = summary.GetProperty("distance").GetDouble(),
            Duration = summary.GetProperty("duration").GetDouble(),
            Elevation = CalculateElevationGain(segments, summary)
        };

        // Direct coordinates array from GeoJSON
        var coordinates = geometry.GetProperty("coordinates");
        routeData.Geometry = coordinates.EnumerateArray()
            .Select(coord => new Shared.Models.RoutePoint
            {
                Longitude = coord[0].GetDouble(),
                Latitude = coord[1].GetDouble(),
                Elevation = coord.GetArrayLength() > 2 ? coord[2].GetDouble() : null
            })
            .ToList();

        // Parse turn-by-turn directions
        var directions = new List<Direction>();
        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.TryGetProperty("steps", out var steps))
            {
                foreach (var step in steps.EnumerateArray())
                {
                    var instruction = step.GetProperty("instruction").GetString() ?? "";
                    var distance = step.GetProperty("distance").GetDouble();
                    var duration = step.GetProperty("duration").GetDouble();
                    var wayPoints = step.GetProperty("way_points").EnumerateArray().ToArray();
                    
                    if (wayPoints.Length > 0)
                    {
                        var pointIndex = wayPoints[0].GetInt32();
                        var location = routeData.Geometry.ElementAtOrDefault(pointIndex) ?? new Shared.Models.RoutePoint();
                        
                        directions.Add(new Shared.Models.Direction
                        {
                            Instruction = instruction,
                            Distance = distance,
                            Duration = duration,
                            Type = ParseDirectionType(step),
                            Location = location
                        });
                    }
                }
            }
        }
        
        routeData.Directions = directions;

        // If elevation was not calculated from segments/summary, try to calculate from geometry
        if (routeData.Elevation == 0 && routeData.Geometry.Any(p => p.Elevation.HasValue))
        {
            _logger.LogInformation("Primary elevation calculation returned 0, trying geometry-based calculation...");
            routeData.Elevation = CalculateElevationGainFromGeometry(routeData.Geometry);
        }
        
        _logger.LogInformation("Final elevation result: {Elevation}m", routeData.Elevation);

        // Calculate bounds
        if (routeData.Geometry.Any())
        {
            routeData.Bounds = new RouteBounds
            {
                MinLatitude = routeData.Geometry.Min(p => p.Latitude),
                MaxLatitude = routeData.Geometry.Max(p => p.Latitude),
                MinLongitude = routeData.Geometry.Min(p => p.Longitude),
                MaxLongitude = routeData.Geometry.Max(p => p.Longitude)
            };
        }

        return routeData;
    }

    private List<AddressResult> ParseGeocodingResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var features = root.GetProperty("features");

        var results = new List<AddressResult>();
        
        foreach (var feature in features.EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            var geometry = feature.GetProperty("geometry");
            var coordinates = geometry.GetProperty("coordinates");

            var result = new AddressResult
            {
                DisplayName = properties.TryGetProperty("label", out var label) ? label.GetString() ?? "" : "",
                Name = properties.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                Address = properties.TryGetProperty("street", out var street) ? street.GetString() : null,
                City = properties.TryGetProperty("locality", out var city) ? city.GetString() : null,
                Country = properties.TryGetProperty("country", out var country) ? country.GetString() : null,
                Longitude = coordinates[0].GetDouble(),
                Latitude = coordinates[1].GetDouble(),
                Type = properties.TryGetProperty("source", out var source) ? source.GetString() ?? "" : ""
            };

            results.Add(result);
        }

        return results;
    }

    private double CalculateElevationGainFromGeometry(List<Shared.Models.RoutePoint> geometry)
    {
        _logger.LogInformation("Calculating elevation from geometry with {Count} points", geometry.Count);
        
        double totalGain = 0;
        int pointsWithElevation = 0;
        
        for (int i = 1; i < geometry.Count; i++)
        {
            var prev = geometry[i - 1];
            var current = geometry[i];
            
            if (prev.Elevation.HasValue && current.Elevation.HasValue)
            {
                pointsWithElevation++;
                var elevationDiff = current.Elevation.Value - prev.Elevation.Value;
                
                if (i <= 5) // Log first few points for debugging
                {
                    _logger.LogInformation("Point {Index}: prev={PrevElevation}m, current={CurrentElevation}m, diff={Diff}m", 
                        i, prev.Elevation.Value, current.Elevation.Value, elevationDiff);
                }
                
                if (elevationDiff > 0) // Only count uphill as elevation gain
                {
                    totalGain += elevationDiff;
                }
            }
        }
        
        _logger.LogInformation("Geometry calculation: {PointsWithElevation}/{TotalPoints} points had elevation data, total gain: {TotalGain}m", 
            pointsWithElevation, geometry.Count, totalGain);
            
        return totalGain;
    }

    private double CalculateElevationGain(JsonElement segments, JsonElement summary)
    {
        // Log the raw summary data for debugging
        _logger.LogInformation("Raw summary data: {Summary}", summary.GetRawText());
        
        // First try to get elevation from summary (ascent property)
        if (summary.TryGetProperty("ascent", out var ascent))
        {
            var elevation = ascent.GetDouble();
            _logger.LogInformation("Found elevation from summary.ascent: {Elevation}m", elevation);
            return elevation;
        }

        // Check for other possible elevation properties in summary
        foreach (var property in summary.EnumerateObject())
        {
            _logger.LogInformation("Summary property: {Name} = {Value}", property.Name, property.Value.GetRawText());
        }

        // If not available in summary, try to calculate from segments
        double totalElevationGain = 0;
        _logger.LogInformation("Segments count: {Count}", segments.GetArrayLength());
        
        foreach (var segment in segments.EnumerateArray())
        {
            // Log segment properties for debugging
            _logger.LogInformation("Segment data: {Segment}", segment.GetRawText());
            
            if (segment.TryGetProperty("ascent", out var segmentAscent))
            {
                var segmentElevation = segmentAscent.GetDouble();
                totalElevationGain += segmentElevation;
                _logger.LogInformation("Found segment ascent: {Elevation}m, total so far: {Total}m", segmentElevation, totalElevationGain);
            }
            
            // Check for other elevation-related properties
            foreach (var property in segment.EnumerateObject())
            {
                if (property.Name.Contains("elevation") || property.Name.Contains("ascent") || property.Name.Contains("descent"))
                {
                    _logger.LogInformation("Segment elevation property: {Name} = {Value}", property.Name, property.Value.GetRawText());
                }
            }
        }

        if (totalElevationGain > 0)
        {
            _logger.LogInformation("Calculated elevation from segments: {Elevation}m", totalElevationGain);
        }
        else
        {
            _logger.LogWarning("No elevation data found in OpenRouteService response");
        }

        // If still no elevation data found, return 0
        // Note: Elevation calculation from geometry coordinates would require 
        // the elevation data to be included in the coordinates array (3D coordinates)
        return totalElevationGain;
    }

    private DirectionType ParseDirectionType(JsonElement step)
    {
        if (step.TryGetProperty("type", out var typeElement))
        {
            var type = typeElement.GetInt32();
            return type switch
            {
                0 => DirectionType.TurnLeft,
                1 => DirectionType.TurnRight,
                2 => DirectionType.TurnSharpLeft,
                3 => DirectionType.TurnSharpRight,
                4 => DirectionType.TurnSlightLeft,
                5 => DirectionType.TurnSlightRight,
                6 => DirectionType.Continue,
                7 => DirectionType.RoundaboutEnter,
                8 => DirectionType.RoundaboutExit,
                9 => DirectionType.UTurn,
                10 => DirectionType.Start,
                11 => DirectionType.End,
                12 => DirectionType.KeepLeft,
                13 => DirectionType.KeepRight,
                _ => DirectionType.Straight
            };
        }
        return DirectionType.Straight;
    }

    private List<RoutePoint> DecodePolyline(string encoded)
    {
        // Robust polyline decoder (Google Encoded Polyline Algorithm Format)
        var points = new List<RoutePoint>();
        if (string.IsNullOrEmpty(encoded))
            return points;

        int index = 0, lat = 0, lng = 0;

        try
        {
            while (index < encoded.Length)
            {
                // Decode latitude
                int result = 0, shift = 0;
                int b;
                do
                {
                    if (index >= encoded.Length) return points; // truncated input
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                // Decode longitude
                result = 0; shift = 0;
                do
                {
                    if (index >= encoded.Length) return points; // truncated input
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lng += dlng;

                points.Add(new Shared.Models.RoutePoint
                {
                    Latitude = lat / 1e5,
                    Longitude = lng / 1e5
                });
            }
        }
        catch
        {
            // swallow malformed input errors and return what we decoded so far
        }

        return points;
    }

    // Haversine distance between (lat1,lon1) and (lat2,lon2) in meters
    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // meters
        double ToRad(double d) => d * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
