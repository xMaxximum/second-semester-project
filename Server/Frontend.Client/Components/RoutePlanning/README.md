# Route Planning Components

This folder contains all components related to route planning functionality in the application.

## Components Overview

### 🗺️ SimpleRoutePlanner
**Files:** `SimpleRoutePlanner.razor`, `SimpleRoutePlanner.razor.cs`, `SimpleRoutePlanner.razor.css`, `SimpleRoutePlanner.razor.js`

The main route planning component that provides:
- Interactive map for route planning
- Waypoint management (add, remove, reorder)
- Address search functionality
- Route calculation with different profiles
- Turn-by-turn directions
- Route statistics (distance, duration, elevation)
- Geolocation support

**Key Features:**
- **Route Profiles:** Cycling, Mountain Bike, Road Bike, Walking, Driving
- **Route Options:** Avoid highways, tolls, ferries
- **Interactive Elements:** Click to add waypoints, drag to reorder
- **Address Search:** Geocoding with auto-complete
- **Responsive Design:** Optimized for mobile and desktop

### 🗂️ RoutePlanner
**Files:** `RoutePlanner.razor`

A wrapper component that provides additional functionality around the SimpleRoutePlanner.

### 🎯 RoutePlannerTile
**Files:** `RoutePlannerTile.razor`

A dashboard tile component for quick access to route planning features.

## Component Architecture

### File Structure
```
RoutePlanning/
├── README.md                           # This documentation
├── SimpleRoutePlanner.razor            # Main UI template
├── SimpleRoutePlanner.razor.cs         # Code-behind logic
├── SimpleRoutePlanner.razor.css        # Scoped styles
├── SimpleRoutePlanner.razor.js         # Client-side JavaScript
├── RoutePlanner.razor                  # Wrapper component
└── RoutePlannerTile.razor              # Dashboard tile
```

### Dependencies
- **Services:** `IRoutePlannerService`, `MapService`, `GeolocationService`
- **UI Framework:** MudBlazor
- **Models:** `Waypoint`, `RouteData`, `RouteProfile`, `DirectionType`
- **JavaScript Interop:** Geolocation API

## Usage Examples

### Basic Usage
```html
<SimpleRoutePlanner 
    MapElementId="my-route-map"
    InitialLatitude="51.505"
    InitialLongitude="-0.09"
    OnRouteCalculated="HandleRouteCalculated" />
```

### With Custom Parameters
```html
<SimpleRoutePlanner 
    MapElementId="custom-map"
    InitialLatitude="@userLatitude"
    InitialLongitude="@userLongitude"
    OnRouteCalculated="@((route) => ProcessRoute(route))" />
```

## Key Features Explained

### 🎯 Route Planning Modes
- **Interactive Mode:** Click on map to add waypoints
- **Search Mode:** Use address search to find and add locations
- **Geolocation:** Start from current user location

### 🚴 Route Profiles
- **Cycling:** General cycling routes
- **Mountain Bike:** Off-road and trail-friendly routes
- **Road Bike:** Road-optimized cycling routes
- **Walking:** Pedestrian-friendly paths
- **Driving:** Car-accessible roads

### 🔧 Route Options
- **Avoid Highways:** Skip major highways
- **Avoid Tolls:** Bypass toll roads
- **Avoid Ferries:** Skip ferry connections

### 📱 Responsive Design
- **Mobile:** Stacked layout, touch-friendly controls
- **Tablet:** Optimized spacing and button sizes
- **Desktop:** Full-featured layout with side-by-side elements

## JavaScript Integration

### Geolocation Service
```javascript
window.blazorGeolocation = {
    getCurrentPosition: function () {
        // Returns Promise with user's current position
        // Handles permission requests and error cases
    }
};
```

### Features
- **High Accuracy:** GPS-level precision when available
- **Error Handling:** User-friendly error messages
- **Timeout Protection:** 15-second timeout with fallback
- **Caching:** 1-minute position cache for performance

## Styling Architecture

### CSS Organization
- **Component Styles:** Scoped to SimpleRoutePlanner component
- **Responsive Design:** Mobile-first approach with breakpoints
- **Theme Integration:** Uses consistent color scheme
- **Animation:** Smooth transitions and hover effects

### Key CSS Classes
- `.route-planner` - Main container
- `.route-planner-header` - Header with gradient background
- `.route-controls` - Button container with flex layout
- `.waypoint-item` - Individual waypoint display
- `.direction-item` - Turn-by-turn direction display

## API Integration

### Route Calculation
```csharp
var request = new RouteRequest
{
    Waypoints = waypoints,
    Profile = RouteProfile.Cycling,
    AvoidHighways = true,
    Language = "en"
};
var response = await RoutePlannerService.CalculateRouteAsync(request);
```

### Address Search
```csharp
var request = new AddressSearchRequest
{
    Query = searchQuery,
    Limit = 5
};
var response = await RoutePlannerService.SearchAddressAsync(request);
```

## Performance Considerations

### Optimization Features
- **Debounced Search:** 300ms delay to prevent excessive API calls
- **Efficient Rendering:** State management optimized for large waypoint lists
- **Memory Management:** Proper disposal of timers and map instances
- **Lazy Loading:** Components only initialize when needed

### Best Practices
- Use `StateHasChanged()` judiciously to minimize re-renders
- Dispose of JavaScript objects and timers properly
- Cache geocoding results when possible
- Implement proper error boundaries

## Troubleshooting

### Common Issues
1. **Map Not Loading:** Check MapService configuration and API keys
2. **Geolocation Failing:** Verify HTTPS and user permissions
3. **Route Calculation Errors:** Ensure routing service API key is configured
4. **Performance Issues:** Check for memory leaks in JavaScript objects

### Debug Tips
- Enable browser developer tools for JavaScript debugging
- Check browser console for geolocation permission errors
- Verify network requests for API call failures
- Use Blazor debugging tools for component state inspection

## Future Enhancements

### Planned Features
- **Route Optimization:** Multi-waypoint optimization
- **Elevation Profiles:** Visual elevation charts
- **Route Sharing:** Export/import route data
- **Offline Support:** Cached map tiles and routing
- **Multi-Modal Routes:** Combined transport methods

### Extension Points
- Custom route profiles
- Plugin architecture for additional map providers
- Custom waypoint types and metadata
- Integration with fitness tracking services

---

## Contributing

When modifying these components:

1. **Follow Blazor Conventions:** Use proper file naming and structure
2. **Maintain Separation:** Keep HTML, CSS, and C# in separate files
3. **Update Documentation:** Keep this README current with changes
4. **Test Responsiveness:** Verify mobile and desktop layouts
5. **Performance Testing:** Check for memory leaks and optimization opportunities

For questions or issues, refer to the main project documentation or contact the development team.
