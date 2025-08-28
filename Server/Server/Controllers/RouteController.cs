using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using Shared.Models;

namespace Server.Controllers;

[ApiController]
[Route(Constants.RoutePrefix + "/routes")]
public class RouteController : ControllerBase
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RouteController> _logger;

    public RouteController(IRouteService routeService, ILogger<RouteController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<RouteResponse>> CalculateRoute([FromBody] RouteRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new RouteResponse
                {
                    Success = false,
                    Error = "Invalid request data"
                });
            }

            _logger.LogInformation("Calculating route with {WaypointCount} waypoints", request.Waypoints.Count);

            var response = await _routeService.CalculateRouteAsync(request);
            
            if (response.Success)
            {
                _logger.LogInformation("Route calculated successfully: {Distance}m, {Duration}s", 
                    response.Route?.Distance, response.Route?.Duration);
            }
            else
            {
                _logger.LogWarning("Route calculation failed: {Error}", response.Error);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating route");
            return StatusCode(500, new RouteResponse
            {
                Success = false,
                Error = "An error occurred while calculating the route"
            });
        }
    }

    [HttpPost("search-address")]
    public async Task<ActionResult<AddressSearchResponse>> SearchAddress([FromBody] AddressSearchRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AddressSearchResponse
                {
                    Success = false,
                    Error = "Invalid request data"
                });
            }

            _logger.LogInformation("Searching addresses for: {Query}", request.Query);

            var response = await _routeService.SearchAddressAsync(request);
            
            _logger.LogInformation("Address search returned {ResultCount} results", response.Results.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching addresses");
            return StatusCode(500, new AddressSearchResponse
            {
                Success = false,
                Error = "An error occurred while searching addresses"
            });
        }
    }

    [HttpGet("saved")]
    [Authorize]
    public async Task<ActionResult<List<SavedRoute>>> GetSavedRoutes()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized("User not authenticated");
            }

            var routes = await _routeService.GetSavedRoutesAsync(userId.Value);
            return Ok(routes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved routes");
            return StatusCode(500, "An error occurred while getting saved routes");
        }
    }

    [HttpPost("save")]
    [Authorize]
    public async Task<ActionResult<SavedRoute>> SaveRoute([FromBody] SaveRouteRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data");
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Saving route: {RouteName} for user {UserId}", request.Name, userId);

            var savedRoute = await _routeService.SaveRouteAsync(request, userId.Value);
            
            if (savedRoute == null)
            {
                return StatusCode(500, "Failed to save route");
            }

            return Ok(savedRoute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving route");
            return StatusCode(500, "An error occurred while saving the route");
        }
    }

    [HttpDelete("saved/{routeId}")]
    [Authorize]
    public async Task<ActionResult> DeleteSavedRoute(int routeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Deleting route {RouteId} for user {UserId}", routeId, userId);

            var success = await _routeService.DeleteSavedRouteAsync(routeId, userId.Value);
            
            if (!success)
            {
                return NotFound("Route not found or access denied");
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting route");
            return StatusCode(500, "An error occurred while deleting the route");
        }
    }

    private long? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("id")?.Value ?? User.FindFirst("sub")?.Value;
        
        if (long.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
