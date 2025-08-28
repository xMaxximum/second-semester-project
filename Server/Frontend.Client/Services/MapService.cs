using Microsoft.JSInterop;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Models;

public class MapService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private IJSObjectReference? _map;

    private static readonly JsonSerializerOptions _camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MapService(IJSRuntime js) => _js = js;

    public async Task<IJSObjectReference?> InitializeMapAsync(
        string elementId, double centerLat, double centerLon, MapConfiguration config)
    {
        if (!OperatingSystem.IsBrowser()) return null;

        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "/js/map-module.js");

        _map = await _module.InvokeAsync<IJSObjectReference>(
            "initializeMap", elementId, centerLat, centerLon, config);

        return _map;
    }


    public ValueTask AddRouteDataAsync(object[] coordinates)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("addRouteData", new object?[] { coordinates });

    public ValueTask SetTileLayerAsync(string layerType)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("setTileLayer", layerType);

    public ValueTask ToggleClusteringAsync(bool enabled)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("toggleClustering", enabled);

    public ValueTask ShowElevationProfileAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("showElevationProfile");

    // Route planning methods
    public ValueTask EnableRoutePlanningAsync(bool enabled)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("enableRoutePlanning", enabled);

    public ValueTask AddWaypointAsync(double latitude, double longitude, string? name = null)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("addWaypoint", latitude, longitude, name);

    public ValueTask ClearWaypointsAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("clearWaypoints");

    public ValueTask ShowRouteAsync(RouteData route)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("showRoute", route);

    public ValueTask ClearRouteAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("clearRoute");

    public ValueTask ShowDirectionsAsync(List<Direction> directions)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("showDirections", directions);

    public ValueTask<Waypoint[]> GetWaypointsAsync()
        => _map is null ? ValueTask.FromResult(Array.Empty<Waypoint>()) : _map.InvokeAsync<Waypoint[]>("getWaypoints");

    public ValueTask FitBoundsAsync(RouteBounds bounds)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("fitBounds", 
            bounds.MinLatitude, bounds.MaxLatitude, bounds.MinLongitude, bounds.MaxLongitude);

    public ValueTask SetMapCenterAsync(double latitude, double longitude, int zoom = 13)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("setMapCenter", latitude, longitude, zoom);

    public ValueTask SetRoutePlanningCallbacksAsync(object callbacks)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("setRoutePlanningCallbacks", callbacks);

    public ValueTask ClearDirectionsAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("clearDirections");

    public ValueTask ResetRoutePlanningAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("resetRoutePlanning");

    public ValueTask HighlightDirectionAsync(int index)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("highlightDirection", index);

    public ValueTask ClearDirectionHighlightAsync()
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("clearDirectionHighlight");

    public ValueTask FocusOnWaypointAsync(int index)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("focusOnWaypoint", index);

    public ValueTask FocusOnDirectionAsync(int index)
        => _map is null ? ValueTask.CompletedTask : _map.InvokeVoidAsync("focusOnDirection", index);

    public async ValueTask DisposeMapAsync()
    {
        if (_map is not null)
        {
            try { await _map.InvokeVoidAsync("dispose"); } catch { }
            await _map.DisposeAsync();
            _map = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeMapAsync();
        if (_module is not null) { await _module.DisposeAsync(); _module = null; }
    }
}

public class MapConfiguration
{
    public bool ShowMarkers { get; set; } = true;
    public bool EnableClustering { get; set; } = false;
    public string DefaultTileLayer { get; set; } = "osm";
    public bool ShowElevationProfile { get; set; } = false;
    public int ClusterDistance { get; set; } = 80;
    public int MaxZoom { get; set; } = 18;
    public bool ShowSpeedColors { get; set; } = true;
    public bool ShowTemperatureMarkers { get; set; } = true;
    public bool EnableRoutePlanning { get; set; } = false;
    public bool ShowClusteringButton { get; set; } = true;
    public bool ShowElevationButton { get; set; } = true;
}
