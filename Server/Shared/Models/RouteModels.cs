using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Shared.Models;

public class RouteRequest
{
    [Required]
    public List<Waypoint> Waypoints { get; set; } = new();
    
    [Required]
    public RouteProfile Profile { get; set; } = RouteProfile.Cycling;
    
    public bool AvoidTolls { get; set; } = false;
    public bool AvoidHighways { get; set; } = false;
    public bool AvoidFerries { get; set; } = false;
    public string? Language { get; set; } = "en";
}

public class RouteResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public RouteData? Route { get; set; }
}

public class RouteData
{
    public double Distance { get; set; } // in meters
    public double Duration { get; set; } // in seconds
    public double Elevation { get; set; } // elevation gain in meters
    public List<RoutePoint> Geometry { get; set; } = new();
    public List<Direction> Directions { get; set; } = new();
    public RouteBounds Bounds { get; set; } = new();
}

public class Waypoint
{
    [Required]
    public double Latitude { get; set; }
    
    [Required]
    public double Longitude { get; set; }
    
    public string? Name { get; set; }
    public string? Address { get; set; }
}

public class RoutePoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Elevation { get; set; }
    public double? Distance { get; set; } // cumulative distance from start
}

public class Direction
{
    public string Instruction { get; set; } = string.Empty;
    public double Distance { get; set; } // distance for this step in meters
    public double Duration { get; set; } // duration for this step in seconds
    public DirectionType Type { get; set; } = DirectionType.Straight;
    public int? ExitNumber { get; set; } // for roundabouts
    public RoutePoint Location { get; set; } = new();
}

public class RouteBounds
{
    public double MinLatitude { get; set; }
    public double MaxLatitude { get; set; }
    public double MinLongitude { get; set; }
    public double MaxLongitude { get; set; }
}

public enum RouteProfile
{
    Driving,
    Cycling,
    Walking,
    CyclingMountain,
    CyclingRoad,
    CyclingElectric,
    Wheelchair
}

public enum DirectionType
{
    Start,
    End,
    Straight,
    TurnLeft,
    TurnRight,
    TurnSlightLeft,
    TurnSlightRight,
    TurnSharpLeft,
    TurnSharpRight,
    UTurn,
    RoundaboutEnter,
    RoundaboutExit,
    Continue,
    Merge,
    ForkLeft,
    ForkRight,
    KeepLeft,
    KeepRight
}

// Address search models
public class AddressSearchRequest
{
    [Required]
    public string Query { get; set; } = string.Empty;
    
    public int Limit { get; set; } = 5;
    public double? Latitude { get; set; } // for proximity search
    public double? Longitude { get; set; }
    public string? CountryCode { get; set; }
}

public class AddressSearchResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<AddressResult> Results { get; set; } = new();
}

public class AddressResult
{
    public string DisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = string.Empty; // poi, address, etc
}

// Saved route models
public class SavedRoute
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RouteProfile Profile { get; set; }
    public double Distance { get; set; }
    public double Duration { get; set; }
    public double? Elevation { get; set; }
    public List<Waypoint> Waypoints { get; set; } = new();
    public string? RouteData { get; set; } // JSON serialized RouteData
    public DateTime CreatedAt { get; set; }
    public long UserId { get; set; }
}

public class SaveRouteRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    [Required]
    public RouteRequest RouteRequest { get; set; } = new();
    
    [Required]
    public RouteData RouteData { get; set; } = new();
}
