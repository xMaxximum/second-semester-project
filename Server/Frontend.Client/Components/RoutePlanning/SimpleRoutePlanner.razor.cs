using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using Frontend.Client.Services;
using Shared.Models;

namespace Frontend.Client.Components.RoutePlanning;

public partial class SimpleRoutePlanner : IAsyncDisposable
{
    [Parameter] public string MapElementId { get; set; } = "route-planner-map";
    [Parameter] public double InitialLatitude { get; set; } = 51.505;
    [Parameter] public double InitialLongitude { get; set; } = -0.09;
    [Parameter] public EventCallback<Shared.Models.RouteData> OnRouteCalculated { get; set; }

    [Inject] private IRoutePlannerService RoutePlannerService { get; set; } = default!;
    [Inject] private MapService MapService { get; set; } = default!;
    [Inject] private GeolocationService GeolocationService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<SimpleRoutePlanner> Logger { get; set; } = default!;

    private IJSObjectReference? _mapInstance;
    private List<Waypoint> _waypoints = new();
    private RouteProfile _selectedProfile = RouteProfile.Cycling;
    private bool _avoidHighways = false;
    private bool _avoidTolls = false;
    private bool _avoidFerries = false;
    private bool _routePlanningEnabled = false;
    private bool _calculatingRoute = false;
    private bool _gettingLocation = false;
    private bool _showClustering = true;
    private bool _showElevation = true;
    private Shared.Models.RouteData? _currentRoute;
    private string _errorMessage = string.Empty;
    
    private string _searchQuery = string.Empty;
    private List<AddressResult> _searchResults = new();
    private System.Threading.Timer? _searchTimer;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeMap();
        }
    }

    private async Task InitializeMap()
    {
        try
        {
            var config = new MapConfiguration
            {
                DefaultTileLayer = "osm",
                ShowMarkers = false,
                EnableClustering = false,
                ShowSpeedColors = false,
                EnableRoutePlanning = true,
                ShowClusteringButton = false, // Hide clustering button in route planner
                ShowElevationButton = false   // Hide elevation button in route planner
            };

            _mapInstance = await MapService.InitializeMapAsync(MapElementId, InitialLatitude, InitialLongitude, config);
            
            if (_mapInstance != null)
            {
                await MapService.EnableRoutePlanningAsync(false); // Start disabled

                // Register .NET callbacks directly via interop
                var dotNetRef = DotNetObjectReference.Create(this);
                await MapService.SetRoutePlanningCallbacksAsync(new { dotNetRef });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize route planner map");
            _errorMessage = "Failed to initialize map";
            StateHasChanged();
        }
    }

    private async Task ToggleRoutePlanning()
    {
        _routePlanningEnabled = !_routePlanningEnabled;

        if (_mapInstance != null)
        {
            await MapService.EnableRoutePlanningAsync(_routePlanningEnabled);
            if (!_routePlanningEnabled)
            {
                await MapService.ResetRoutePlanningAsync();
                _waypoints.Clear();
                _currentRoute = null;
                _errorMessage = string.Empty;
            }
        }

        StateHasChanged();
    }

    private async Task StartFromMyLocation()
    {
        try
        {
            _gettingLocation = true;
            StateHasChanged();
            
            var locationResult = await GeolocationService.GetCurrentPositionAsync();
            
            if (locationResult.IsSuccess)
            {
                var waypoint = new Waypoint
                {
                    Latitude = locationResult.Latitude,
                    Longitude = locationResult.Longitude,
                    Name = "My Location"
                };
                
                _waypoints.Clear();
                _waypoints.Add(waypoint);
                
                if (_mapInstance != null)
                {
                    await MapService.ClearWaypointsAsync();
                    await MapService.AddWaypointAsync(waypoint.Latitude, waypoint.Longitude, waypoint.Name);
                    
                    // Center map on user location
                    await MapService.SetMapCenterAsync(waypoint.Latitude, waypoint.Longitude, 15);
                }
                
                // Enable route planning after adding starting point
                _routePlanningEnabled = true;
                if (_mapInstance != null)
                {
                    await MapService.EnableRoutePlanningAsync(_routePlanningEnabled);
                }
            }
            else
            {
                _errorMessage = locationResult.ErrorMessage ?? "Could not get your location. Please add waypoints manually.";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error getting location: {ex.Message}";
            Logger.LogError(ex, "Error getting user location for route planning");
        }
        finally
        {
            _gettingLocation = false;
            StateHasChanged();
        }
    }

    private async Task ClearWaypoints()
    {
        _waypoints.Clear();
        _currentRoute = null;

        if (_mapInstance != null)
        {
            await MapService.ClearWaypointsAsync();
            await MapService.ClearRouteAsync();
            await MapService.ClearDirectionsAsync();
            
            // Stop planning mode when clearing
            if (_routePlanningEnabled)
            {
                _routePlanningEnabled = false;
                await MapService.EnableRoutePlanningAsync(false);
            }
        }

        StateHasChanged();
    }

    private async Task RemoveWaypoint(int index)
    {
        if (index >= 0 && index < _waypoints.Count)
        {
            _waypoints.RemoveAt(index);
            
            // Update waypoint names
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i].Name?.StartsWith("Waypoint ") == true)
                {
                    _waypoints[i].Name = $"Waypoint {i + 1}";
                }
            }
            
            if (_mapInstance != null)
            {
                await MapService.ClearWaypointsAsync();
                foreach (var waypoint in _waypoints)
                {
                    await MapService.AddWaypointAsync(waypoint.Latitude, waypoint.Longitude, waypoint.Name);
                }
            }
            
            // Recalculate route if we still have enough waypoints
            if (_waypoints.Count >= 2 && _currentRoute != null)
            {
                await CalculateRoute();
            }
            else
            {
                _currentRoute = null;
                if (_mapInstance != null)
                {
                    await MapService.ClearRouteAsync();
                }
            }
            
            StateHasChanged();
        }
    }

    private async Task HighlightDirection(int index)
    {
        if (_mapInstance != null)
        {
            await MapService.HighlightDirectionAsync(index);
        }
    }

    private async Task ClearDirectionHighlight()
    {
        if (_mapInstance != null)
        {
            await MapService.ClearDirectionHighlightAsync();
        }
    }

    private async Task FocusOnWaypoint(int index)
    {
        if (_mapInstance != null)
        {
            await MapService.FocusOnWaypointAsync(index);
        }
    }

    private async Task FocusOnDirection(int index)
    {
        if (_mapInstance != null)
        {
            await MapService.FocusOnDirectionAsync(index);
        }
    }

    private async Task SearchAddress()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults.Clear();
            StateHasChanged();
            return;
        }

        try
        {
            var request = new AddressSearchRequest
            {
                Query = _searchQuery,
                Limit = 5
            };

            var response = await RoutePlannerService.SearchAddressAsync(request);
            
            if (response.Success)
            {
                _searchResults = response.Results;
            }
            else
            {
                _errorMessage = response.Error ?? "Address search failed";
                _searchResults.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching address");
            _errorMessage = "Error searching address";
            _searchResults.Clear();
        }
        
        StateHasChanged();
    }

    private async Task OnSearchKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchAddress();
        }
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        _searchQuery = e.Value?.ToString() ?? string.Empty;
        
        // Debounce search - wait 300ms after user stops typing
        _searchTimer?.Dispose();
        _searchTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(_searchQuery) && _searchQuery.Length >= 2)
                {
                    await SearchAddress();
                }
                else
                {
                    _searchResults.Clear();
                    StateHasChanged();
                }
            });
        }, null, 300, Timeout.Infinite);
    }

    private async Task AddWaypointFromSearch(AddressResult result)
    {
        var waypoint = new Waypoint
        {
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Name = result.DisplayName,
            Address = result.Address
        };

        _waypoints.Add(waypoint);
        _searchResults.Clear();
        _searchQuery = string.Empty;

        if (_mapInstance != null)
        {
            await MapService.AddWaypointAsync(waypoint.Latitude, waypoint.Longitude, waypoint.Name);
        }

        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnWaypointAdded(double latitude, double longitude, string? name = null)
    {
        try
        {
            var waypoint = new Waypoint
            {
                Latitude = latitude,
                Longitude = longitude,
                Name = name ?? $"Waypoint {_waypoints.Count + 1}"
            };

            _waypoints.Add(waypoint);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding waypoint from map click");
        }
    }

    [JSInvokable]
    public Task OnWaypointChanged(int index, double latitude, double longitude, string? name = null)
    {
        if (index >= 0 && index < _waypoints.Count)
        {
            _waypoints[index].Latitude = latitude;
            _waypoints[index].Longitude = longitude;
            if (!string.IsNullOrWhiteSpace(name)) _waypoints[index].Name = name;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnWaypointRemoved(int index)
    {
        if (index >= 0 && index < _waypoints.Count)
        {
            _waypoints.RemoveAt(index);
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i].Name?.StartsWith("Waypoint ") == true)
                {
                    _waypoints[i].Name = $"Waypoint {i + 1}";
                }
            }
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private async Task CalculateRoute()
    {
        if (_waypoints.Count < 2)
        {
            _errorMessage = "At least 2 waypoints are required";
            return;
        }

        _calculatingRoute = true;
        _errorMessage = string.Empty;
        StateHasChanged();

        try
        {
            var request = new RouteRequest
            {
                Waypoints = _waypoints,
                Profile = _selectedProfile,
                AvoidHighways = _avoidHighways,
                AvoidTolls = _avoidTolls,
                AvoidFerries = _avoidFerries,
                Language = "en"
            };

            var response = await RoutePlannerService.CalculateRouteAsync(request);

            if (response.Success && response.Route != null)
            {
                _currentRoute = response.Route;
                
                if (_mapInstance != null)
                {
                    await MapService.ShowRouteAsync(_currentRoute);
                    
                    if (_currentRoute.Directions?.Any() == true)
                    {
                        await MapService.ShowDirectionsAsync(_currentRoute.Directions);
                    }
                    
                    if (_currentRoute.Bounds != null)
                    {
                        await MapService.FitBoundsAsync(_currentRoute.Bounds);
                    }
                }

                await OnRouteCalculated.InvokeAsync(_currentRoute);
            }
            else
            {
                _errorMessage = response.Error ?? "Route calculation failed";
                _currentRoute = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating route");
            _errorMessage = "Error calculating route. Note: This demo requires a routing service API key to be configured.";
            _currentRoute = null;
        }
        finally
        {
            _calculatingRoute = false;
            StateHasChanged();
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
        StateHasChanged();
    }

    private string FormatDuration(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }
        if (time.TotalMinutes >= 1)
        {
            return $"{time.Minutes}m {time.Seconds}s";
        }
        return $"{time.Seconds}s";
    }

    private string FormatDistance(double meters)
    {
        if (meters < 1)
        {
            return "0 m";
        }
        if (meters < 1000)
        {
            return $"{Math.Round(meters):0} m";
        }
        if (meters < 10000)
        {
            return $"{meters / 1000:0.0} km";
        }
        return $"{meters / 1000:0} km";
    }

    private string GetDirectionIcon(DirectionType type)
    {
        return type switch
        {
            DirectionType.Start => "🟢",
            DirectionType.End => "🔴",
            DirectionType.TurnLeft => "↰",
            DirectionType.TurnRight => "↱",
            DirectionType.TurnSlightLeft => "↖",
            DirectionType.TurnSlightRight => "↗",
            DirectionType.TurnSharpLeft => "↺",
            DirectionType.TurnSharpRight => "↻",
            DirectionType.UTurn => "🔄",
            DirectionType.RoundaboutEnter => "🔄",
            DirectionType.RoundaboutExit => "↗",
            DirectionType.Continue => "↑",
            DirectionType.Merge => "🔀",
            DirectionType.ForkLeft => "↖",
            DirectionType.ForkRight => "↗",
            DirectionType.KeepLeft => "↖",
            DirectionType.KeepRight => "↗",
            _ => "→"
        };
    }

    public async ValueTask DisposeAsync()
    {
        _searchTimer?.Dispose();
        
        if (_mapInstance != null)
        {
            await MapService.DisposeMapAsync();
            _mapInstance = null;
        }
    }
}
