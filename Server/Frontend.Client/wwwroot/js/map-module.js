let leafletLoaded = false;
let leafletLoadPromise = null;

// Keep one CyclingMap/API per elementId, and dedupe concurrent inits
const instanceApis = new Map();        // elementId -> API object
const instancePromises = new Map();    // elementId -> Promise<API>

// CDN URLs
const CDN_URLS = {
  leafletCss: 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css',
  leafletJs:  'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js',
  markerClusterCss: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css',
  markerClusterDefaultCss: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css',
  markerClusterJs: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js',
  d3: 'https://unpkg.com/d3@6.7.0/dist/d3.min.js',              // <- d3 v6 is safest with the plugin
  elevationCss: 'https://unpkg.com/@raruto/leaflet-elevation/dist/leaflet-elevation.css',
  elevationJs:  'https://unpkg.com/@raruto/leaflet-elevation/dist/leaflet-elevation.js' // <- UMD
};

// Tile layers
const TILE_LAYERS = {
    osm: {
        name: 'OpenStreetMap',
        url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    },
    satellite: {
        name: 'Satellite (Esri)',
        url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        attribution: 'Tiles © Esri — Source: Esri & others',
        maxZoom: 19
    },
    cyclemap: {
        name: 'OpenCycleMap',
        url: () => {
            const key = window.THUNDERFOREST_KEY || 'fcb32f4c3c704ed1a01dd8c0793f07d0';
            return key && key !== 'YOUR_API_KEY'
                ? `https://tile.thunderforest.com/cycle/{z}/{x}/{y}.png?apikey=${key}`
                : null;
        },
        attribution: 'Maps © <a href="https://www.thunderforest.com">Thunderforest</a>, Data © OSM contributors',
        maxZoom: 19,
        requiresApiKey: true
    },
    topo: {
        name: 'OpenTopoMap',
        url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
        attribution: 'Map data © OSM contributors, SRTM — Style © <a href="https://opentopomap.org">OpenTopoMap</a>',
        maxZoom: 19
    }
};

// Helpers to load CSS/JS
function loadCSS(href) {
    return new Promise((resolve, reject) => {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        link.onload = resolve;
        link.onerror = reject;
        document.head.appendChild(link);
    });
}
function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

// Load Leaflet + plugins (compat-safe)
async function loadLeaflet() {
  if (leafletLoaded) return;
  if (leafletLoadPromise) return leafletLoadPromise;

  leafletLoadPromise = (async () => {
    // CSS first
    await Promise.all([
      loadCSS(CDN_URLS.leafletCss),
      loadCSS(CDN_URLS.markerClusterCss),
      loadCSS(CDN_URLS.markerClusterDefaultCss),
      loadCSS(CDN_URLS.elevationCss)
    ]);

    // Scripts in order
    await loadScript(CDN_URLS.leafletJs);
    await loadScript(CDN_URLS.markerClusterJs);
    await loadScript(CDN_URLS.d3);
    await loadScript(CDN_URLS.elevationJs); // <- classic UMD

    if (!window.L || !window.L.control || !window.L.control.elevation) {
      throw new Error('leaflet-elevation failed to attach to L');
    }

    leafletLoaded = true;
    console.log('✅ Leaflet + MarkerCluster + D3 + Elevation loaded');
  })();

  return leafletLoadPromise;
}

// Color helpers
function getSpeedColor(speed) {
    if (speed < 10) return '#ff0000';
    if (speed < 20) return '#ff6600';
    if (speed < 30) return '#ffcc00';
    if (speed < 40) return '#00ff00';
    return '#0066ff';
}
function getTemperatureColor(temp) {
    if (temp < 0) return '#0066ff';
    if (temp < 10) return '#00ccff';
    if (temp < 20) return '#00ff00';
    if (temp < 30) return '#ffcc00';
    return '#ff0000';
}

// Route polyline colored by speed
function createSpeedRoute(coordinates) {
    const segments = [];
    for (let i = 0; i < coordinates.length - 1; i++) {
        const a = coordinates[i];
        const b = coordinates[i + 1];
        const avgSpeed = ((a.speed || 0) + (b.speed || 0)) / 2;
        const poly = L.polyline([[a.latitude, a.longitude], [b.latitude, b.longitude]], {
            color: getSpeedColor(avgSpeed),
            weight: 4,
            opacity: 0.8
        });
        segments.push(poly);
    }
    return L.layerGroup(segments);
}

// Markers + optional cluster
function createCyclingMarkers(coordinates, enableClustering = true, clusterDistance = 80) {
    const markers = [];

    const sampled = coordinates.filter((_, i) => i % 10 === 0);

    sampled.forEach((p, idx) => {
        const isStart = idx === 0;
        const isEnd = idx === sampled.length - 1;

        let html, cls;
        if (isStart) {
            html = '🚴‍♂️';
            cls = 'cycling-marker start-marker';
        } else if (isEnd) {
            html = '🏁';
            cls = 'cycling-marker end-marker';
        } else {
            const c = getTemperatureColor(p.temperature);
            html = `<div style="background-color:${c};width:12px;height:12px;border-radius:50%;border:2px solid #fff;"></div>`;
            cls = 'cycling-marker data-marker';
        }

        const m = L.marker([p.latitude, p.longitude], {
            icon: L.divIcon({ html, className: cls, iconSize: [20, 20], iconAnchor: [10, 10] })
        });

        const popup = `
      <div class="cycling-popup">
        <h4>${isStart ? '🚴‍♂️ Start' : isEnd ? '🏁 Finish' : '📍 Data Point'}</h4>
        <div><strong>Speed:</strong> ${(p.speed ?? 0).toFixed(1)} km/h</div>
        <div><strong>Temperature:</strong> ${(p.temperature ?? 0).toFixed(1)}°C</div>
        <div><strong>Elevation:</strong> ${(p.elevation ?? 0).toFixed(0)} m</div>
        <div><strong>Time:</strong> ${p.timestamp ? new Date(p.timestamp).toLocaleTimeString() : '-'}</div>
        ${p.acceleration ? `<div><strong>Acceleration:</strong> ${p.acceleration.toFixed(2)} m/s²</div>` : ''}
        <div class="coordinates"><small>${p.latitude.toFixed(6)}, ${p.longitude.toFixed(6)}</small></div>
      </div>`;
        m.bindPopup(popup);
        markers.push(m);
    });

    if (enableClustering && window.L.markerClusterGroup) {
        const cluster = window.L.markerClusterGroup({
            chunkedLoading: true,
            maxClusterRadius: clusterDistance,
            iconCreateFunction(cluster) {
                const count = cluster.getChildCount();
                return L.divIcon({
                    html: `<div class="cycling-cluster"><span>${count}</span></div>`,
                    className: 'cycling-cluster-marker',
                    iconSize: [30, 30]
                });
            }
        });
        cluster.addLayers(markers);
        return cluster;
    }

    return L.layerGroup(markers);
}

function createElevationProfile(map, coordinates) {
  if (!window.L.control || !window.L.control.elevation) {
    console.warn('Elevation plugin not loaded');
    return null;
  }

  const elevationControl = L.control.elevation({
    position: 'bottomright',
    theme: 'lightblue-theme',
    detached: true,
    elevationDiv: '#elevation-profile',
    imperial: false,
    reverseCoords: false,               // we're passing [lon,lat,ele]
    preferCanvas: true,
    summary: 'inline',
    autofitBounds: false,               // we fit bounds ourselves
    legend: false                       // disable legend
  }).addTo(map);

  // Build a proper GeoJSON "Feature" with 3D coordinates
  const feature = {
    type: 'Feature',
    properties: { name: 'Track' },
    geometry: {
      type: 'LineString',
      coordinates: coordinates.map(p => [
        p.longitude,
        p.latitude,
        p.elevation
      ])
    }
  };

  elevationControl.addData(feature);
  return elevationControl;
}


// Map wrapper
class CyclingMap {
    constructor(elementId, centerLat, centerLon, config) {
        this.elementId = elementId;
        this.config = config ?? {};
        this.map = null;
        this.layers = { route: null, markers: null, elevation: null, plannedRoute: null, waypoints: null };
        this.currentTileLayer = null;
        this.coordinates = [];
        this.waypoints = [];
        this.routePlanningEnabled = false;
        this.routePlanningCallbacks = {};

        this.initializeMap(centerLat, centerLon);
        this.addCustomCSS();
        
        // Initialize route planning if enabled
        if (this.config.EnableRoutePlanning) {
            this.enableRoutePlanning(true);
        }
    }

    initializeMap(centerLat, centerLon) {
        const el = document.getElementById(this.elementId);
        if (!el) throw new Error(`Map container #${this.elementId} not found`);

        const isMobile = window.innerWidth <= 768;

        this.map = L.map(this.elementId, {
            center: [centerLat, centerLon],
            zoom: 13,
            zoomControl: false,
            attributionControl: true,
            tap: !isMobile,
            touchZoom: true,
            doubleClickZoom: true,
            scrollWheelZoom: true,
            boxZoom: true,
            keyboard: true,
            dragging: true
        });

        L.control.zoom({ position: isMobile ? 'bottomright' : 'topleft' }).addTo(this.map);

        this.setTileLayer(this.config.defaultTileLayer || 'osm');
        this.addLayerControl();
        this.addCustomControls();

        setTimeout(() => this.map && this.map.invalidateSize(), 0);
        window.addEventListener('resize', () => setTimeout(() => this.map && this.map.invalidateSize(), 100));
    }

    addCustomCSS() {
        const style = document.createElement('style');
        style.textContent = `
      .cycling-popup { font-family: Arial, sans-serif; min-width:200px; max-width:300px; }
      .cycling-popup h4 { margin:0 0 10px 0; color:#2c3e50; }
      .cycling-popup div { margin:3px 0; font-size:14px; }
      .cycling-popup .coordinates { color:#7f8c8d; border-top:1px solid #ecf0f1; padding-top:5px; margin-top:8px; }
      .cycling-cluster { background:linear-gradient(135deg,#667eea 0%,#764ba2 100%); border:3px solid #fff; border-radius:50%; color:#fff; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:12px; box-shadow:0 2px 5px rgba(0,0,0,.3); }
      .cycling-marker { filter:drop-shadow(0 2px 4px rgba(0,0,0,.3)); }
      .start-marker, .end-marker { font-size:16px; }
      
      /* Route planning waypoint markers */
      .waypoint-marker { filter:drop-shadow(0 2px 4px rgba(0,0,0,.4)); }
      .start-waypoint { font-size: 18px; }
      .end-waypoint { font-size: 18px; }
      .intermediate-waypoint div {
        box-shadow: 0 2px 6px rgba(0,0,0,.3);
        transition: transform 0.2s ease;
      }
      .intermediate-waypoint div:hover {
        transform: scale(1.1);
      }
      
      /* Route line styling */
      .planned-route {
        stroke-linecap: round;
        stroke-linejoin: round;
      }
      
      /* Custom controls positioned at bottom-left */
      .leaflet-bottom .leaflet-left .map-controls { 
        margin-bottom: 10px; 
        margin-left: 10px; 
        display: flex; 
        flex-direction: row; 
        gap: 8px; 
        flex-wrap: wrap;
      }
      
      .map-control-btn { 
        background:#fff; 
        border:2px solid rgba(0,0,0,.2); 
        border-radius:4px; 
        padding:8px 12px; 
        cursor:pointer; 
        font-size:12px; 
        font-weight: 500;
        box-shadow:0 1px 5px rgba(0,0,0,.3); 
        min-width:110px; 
        text-align:center;
        transition: all 0.2s ease;
        white-space: nowrap;
      }
      
      .map-control-btn:hover { 
        background:#f4f4f4; 
        transform: translateY(-1px);
        box-shadow:0 2px 8px rgba(0,0,0,.4);
      }
      
      .map-control-btn.active { 
        background:#667eea; 
        color:#fff; 
        border-color: #667eea;
        box-shadow:0 2px 8px rgba(102, 126, 234, 0.4);
      }
      
      #elevation-profile { 
        position:relative; 
        background:#fff; 
        border-radius:0; 
        margin-top:10px;
        width:100%;
        box-sizing:border-box;
      }
      
      
      /* Tablet styles */
      @media (max-width:768px) { 
        .cycling-popup { min-width:150px; max-width:250px; }
        .cycling-popup div { font-size:12px; }
        
        .leaflet-bottom .leaflet-left .map-controls { 
          margin-bottom: 10px; 
          margin-left: 10px; 
          gap: 8px; 
          flex-direction: row;
          flex-wrap: wrap;
          max-width: calc(100vw - 20px);
        }
        
        .map-control-btn { 
          padding: 10px 14px; 
          font-size: 12px; 
          min-width: 110px;
          flex: 1;
          max-width: 180px;
          text-align: center;
          touch-action: manipulation;
          -webkit-tap-highlight-color: transparent;
          transition: all 0.15s ease;
        }
        
        .map-control-btn:hover {
          transform: translateY(-1px);
        }
        
        .map-control-btn:active {
          transform: scale(0.96);
          background: #e0e0e0;
          transition: all 0.1s ease;
        }
        
        .map-control-btn.active:active {
          background: #5a6fd8;
        }
        
        #elevation-profile { 
          margin-top: 10px; 
          width: 100%; 
          max-width: none; 
          max-height: 40vh;
          overflow: auto;
        }
      }
      
      /* Mobile styles - show compact buttons */
      @media (max-width:600px) { 
        .cycling-popup { min-width:120px; max-width:200px; }
        .cycling-popup div { font-size:11px; }
        
        .leaflet-bottom .leaflet-left .map-controls { 
          margin-bottom: 8px; 
          margin-left: 8px; 
          gap: 6px; 
          flex-direction: row;
          align-items: center;
          max-width: calc(100vw - 16px);
        }
        
        .map-control-btn { 
          padding: 12px; 
          font-size: 18px; 
          min-width: 48px;
          width: 48px;
          height: 48px;
          flex: none;
          border-radius: 8px;
          display: flex;
          align-items: center;
          justify-content: center;
          touch-action: manipulation;
          -webkit-tap-highlight-color: transparent;
          transition: all 0.15s ease;
          white-space: nowrap;
          overflow: hidden;
        }
        
        .map-control-btn:hover {
          transform: translateY(-2px);
          box-shadow: 0 4px 12px rgba(0,0,0,.3);
        }
        
        .map-control-btn:active {
          transform: scale(0.92);
          background: #e0e0e0;
          transition: all 0.1s ease;
        }
        
        .map-control-btn.active:active {
          background: #5a6fd8;
        }
        
        .start-marker, .end-marker { font-size:14px; }
        
        #elevation-profile { 
          margin-top: 8px; 
          max-height: 35vh;
          overflow: auto;
        }
      }
      
      /* Extra small screens - even more compact */
      @media (max-width:400px) {
        .leaflet-bottom .leaflet-left .map-controls {
          margin-left: 6px;
          margin-bottom: 6px;
          gap: 4px;
          max-width: calc(100vw - 12px);
        }
        
        .map-control-btn {
          font-size: 16px;
          padding: 10px;
          min-width: 44px;
          width: 44px;
          height: 44px;
          border-radius: 6px;
        }
        
        #elevation-profile {
          margin-top: 6px;
          max-height: 30vh;
        }
      }
    `;
        document.head.appendChild(style);
    }

    setTileLayer(layerType) {
        const cfg = TILE_LAYERS[layerType] || TILE_LAYERS.osm;
        const url = typeof cfg.url === 'function' ? cfg.url() : cfg.url;
        if (!url) { 
            console.warn(`Layer "${layerType}" unavailable (missing key)`); 
            return; 
        }

        if (this.currentTileLayer) {
            this.map.removeLayer(this.currentTileLayer);
        }
        
        // Force refresh by clearing tile cache and creating new layer
        this.currentTileLayer = L.tileLayer(url, { 
            attribution: cfg.attribution, 
            maxZoom: cfg.maxZoom,
            // Force refresh tiles - add cache busting and immediate load
            updateWhenIdle: false,
            updateWhenZooming: true,
            keepBuffer: 0 // Don't keep old tiles in buffer
        });
        
        this.currentTileLayer.addTo(this.map);
        
        // Force immediate tile refresh
        setTimeout(() => {
            if (this.map) {
                this.map.invalidateSize();
                // Force redraw of all visible tiles
                this.currentTileLayer.redraw();
            }
        }, 100);
    }

    addLayerControl() {
        const baseLayers = {};
        Object.values(TILE_LAYERS).forEach(cfg => {
            const url = typeof cfg.url === 'function' ? cfg.url() : cfg.url;
            if (!url) return;
            baseLayers[cfg.name] = L.tileLayer(url, { attribution: cfg.attribution, maxZoom: cfg.maxZoom });
        });
        L.control.layers(baseLayers, null, { position: 'topright', collapsed: window.innerWidth <= 768 }).addTo(this.map);
    }

    addCustomControls() {
        const controlDiv = L.DomUtil.create('div', 'map-controls');
        L.DomEvent.disableClickPropagation(controlDiv);
        L.DomEvent.disableScrollPropagation(controlDiv);

        const clusterBtn = L.DomUtil.create('button', 'map-control-btn', controlDiv);
        clusterBtn.setAttribute('data-action', 'cluster');
        clusterBtn.setAttribute('data-full-text-on', '📍 Clustering ON');
        clusterBtn.setAttribute('data-full-text-off', '📍 Clustering OFF');
        clusterBtn.setAttribute('data-compact-text', '📍');
        clusterBtn.title = 'Toggle marker clustering';
        
        this.updateButtonText(clusterBtn, this.config.EnableClustering === true);
        if (this.config.EnableClustering === true) {
            clusterBtn.classList.add('active');
        }
        L.DomEvent.on(clusterBtn, 'click', () => {
            this.toggleClustering();
        });

        const elevationBtn = L.DomUtil.create('button', 'map-control-btn', controlDiv);
        elevationBtn.setAttribute('data-action', 'elevation');
        elevationBtn.setAttribute('data-full-text-show', '📊 Show Elevation');
        elevationBtn.setAttribute('data-full-text-hide', '📊 Hide Elevation');
        elevationBtn.setAttribute('data-compact-text', '📊');
        elevationBtn.title = 'Show elevation profile';
        
        this.updateButtonText(elevationBtn, false);
        L.DomEvent.on(elevationBtn, 'click', () => {
            this.showElevationProfile();
        });

        const Custom = L.Control.extend({ onAdd: () => controlDiv });
        new Custom({ position: 'bottomleft' }).addTo(this.map);
        
        // Update button text on resize
        this.setupResponsiveButtonText();
    }

    setupResponsiveButtonText() {
        const updateAllButtons = () => {
            const clusterBtn = document.querySelector('[data-action="cluster"]');
            const elevationBtn = document.querySelector('[data-action="elevation"]');
            
            if (clusterBtn) {
                this.updateButtonText(clusterBtn, clusterBtn.classList.contains('active'));
            }
            if (elevationBtn) {
                this.updateButtonText(elevationBtn, elevationBtn.classList.contains('active'));
            }
        };

        // Update on resize with debouncing
        let resizeTimeout;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(updateAllButtons, 100);
        });
        
        // Initial update
        setTimeout(updateAllButtons, 0);
    }

    updateButtonText(button, isActive) {
        const isMobile = window.innerWidth <= 600;
        
        if (isMobile) {
            button.innerHTML = button.getAttribute('data-compact-text');
        } else {
            const action = button.getAttribute('data-action');
            if (action === 'cluster') {
                button.innerHTML = isActive ? 
                    button.getAttribute('data-full-text-on') : 
                    button.getAttribute('data-full-text-off');
            } else if (action === 'elevation') {
                button.innerHTML = isActive ? 
                    button.getAttribute('data-full-text-hide') : 
                    button.getAttribute('data-full-text-show');
            }
        }
    }

    addRouteData(coordinates) {
        // Accept array or varargs
        const coords = Array.isArray(coordinates) ? coordinates : Array.from(arguments);

        this.coordinates = coords.map(c => ({
            latitude: c.latitude,
            longitude: c.longitude,
            elevation: c.elevation,
            speed: c.speed,
            temperature: c.temperature,
            timestamp: c.timestamp,
            acceleration: Math.sqrt(
                (c.accelerationX || 0) ** 2 +
                (c.accelerationY || 0) ** 2 +
                (c.accelerationZ || 0) ** 2
            )
        }));
        
        // Debug elevation data
        const elevationValues = this.coordinates.map(c => c.elevation).filter(e => e !== 0);
        console.log(`🏔️ Elevation data: ${elevationValues.length} non-zero values out of ${this.coordinates.length} total points`);
        if (elevationValues.length > 0) {
            console.log(`🏔️ Elevation range: ${Math.min(...elevationValues).toFixed(1)}m to ${Math.max(...elevationValues).toFixed(1)}m`);
        }
        
        if (this.coordinates.length === 0) return;

        // Clear old layers
        this.clearLayers();

        if (this.config.ShowSpeedColors !== false) {
            this.layers.route = createSpeedRoute(this.coordinates);
            this.layers.route.addTo(this.map);
        }

        if (this.config.ShowMarkers !== false) {
            this.layers.markers = createCyclingMarkers(
                this.coordinates,
                this.config.EnableClustering === true,  // Changed: now defaults to false
                this.config.ClusterDistance ?? 80
            );
            this.layers.markers.addTo(this.map);
        }

        // SAFE BOUNDS (no FeatureGroup mixing)
        const bounds = L.latLngBounds([]);

        if (this.layers.route && this.layers.route.eachLayer) {
            this.layers.route.eachLayer(seg => {
                if (seg.getLatLngs) {
                    const latlngs = seg.getLatLngs();
                    const flat = Array.isArray(latlngs[0]) ? latlngs.flat() : latlngs;
                    bounds.extend(L.latLngBounds(flat));
                }
            });
        }

        if (this.layers.markers) {
            if (this.layers.markers.getBounds) {
                bounds.extend(this.layers.markers.getBounds());
            } else if (this.layers.markers.eachLayer) {
                this.layers.markers.eachLayer(m => { if (m.getLatLng) bounds.extend(m.getLatLng()); });
            }
        }

        if (bounds.isValid()) this.map.fitBounds(bounds, { padding: [20, 20] });

        if (this.config.ShowElevationProfile) this.ShowElevationProfile();
    }

    toggleClustering() {
        if (!this.layers.markers || this.coordinates.length === 0) return;
        
        this.map.removeLayer(this.layers.markers);
        this.config.EnableClustering = !this.config.EnableClustering;
        this.layers.markers = createCyclingMarkers(
            this.coordinates,
            this.config.EnableClustering,
            this.config.ClusterDistance ?? 80
        );
        this.layers.markers.addTo(this.map);
        
        // Update button state to reflect CURRENT clustering state
        const clusterBtn = document.querySelector('[data-action="cluster"]');
        if (clusterBtn) {
            if (this.config.EnableClustering) {
                clusterBtn.classList.add('active');
            } else {
                clusterBtn.classList.remove('active'); 
            }
            this.updateButtonText(clusterBtn, this.config.EnableClustering);
        }
    }

    showElevationProfile() {
        if (this.coordinates.length === 0) return;

        const mapContainer = document.getElementById(this.elementId);
        if (!mapContainer) return;

        let div = document.getElementById('elevation-profile');
        if (!div) {
            div = document.createElement('div');
            div.id = 'elevation-profile';
            div.style.display = 'none';
            
            // Append to the map container's parent to sit below the map
            mapContainer.parentElement.appendChild(div);
        }

        const isCurrentlyVisible = div.style.display !== 'none';
        
        if (!isCurrentlyVisible && !this.layers.elevation) {
            this.layers.elevation = createElevationProfile(this.map, this.coordinates);
        }
        
        // Toggle visibility
        div.style.display = isCurrentlyVisible ? 'none' : 'block';
        
        // Update button state to reflect CURRENT state (not inverted)
        const elevationBtn = document.querySelector('[data-action="elevation"]');
        if (elevationBtn) {
            const isActive = div.style.display === 'block';
            if (isActive) {
                elevationBtn.classList.add('active');
            } else {
                elevationBtn.classList.remove('active');
            }
            this.updateButtonText(elevationBtn, isActive);
        }
    }

    clearLayers() {
        for (const k of Object.keys(this.layers)) {
            if (this.layers[k] && k !== 'plannedRoute' && k !== 'waypoints' && k !== 'directions') {
                this.map.removeLayer(this.layers[k]);
                this.layers[k] = null;
            }
        }
    }

    dispose() {
        this.clearLayers();
        this.routePlanningEnabled = false;
        this.waypoints = [];
        if (this.map) { this.map.remove(); this.map = null; }
        const div = document.getElementById('elevation-profile');
        if (div) div.remove();
    }

    // Route planning functionality
    enableRoutePlanning(enabled) {
        this.routePlanningEnabled = enabled;
        
        if (enabled) {
            this.map.getContainer().style.cursor = 'crosshair';
            this.map.on('click', this.onMapClick, this);
        } else {
            this.map.getContainer().style.cursor = '';
            this.map.off('click', this.onMapClick, this);
        }
    }

    onMapClick(e) {
        if (!this.routePlanningEnabled) return;
        
        const waypoint = {
            latitude: e.latlng.lat,
            longitude: e.latlng.lng,
            name: `Waypoint ${this.waypoints.length + 1}`
        };
        
        this.addWaypoint(waypoint.latitude, waypoint.longitude, waypoint.name);
        
        // Notify Blazor component about waypoint added
        if (this.routePlanningCallbacks.onWaypointAdded) {
            this.routePlanningCallbacks.onWaypointAdded(waypoint);
        }
    }

    addWaypoint(latitude, longitude, name = null) {
        const waypoint = {
            latitude,
            longitude,
            name: name || `Waypoint ${this.waypoints.length + 1}`
        };
        
        this.waypoints.push(waypoint);
        this.updateWaypointMarkers();
        
        return waypoint;
    }

    clearWaypoints() {
        this.waypoints = [];
        this.updateWaypointMarkers();
    this.clearRoute();
    this.clearDirections();
    }

    getWaypoints() {
        return [...this.waypoints];
    }

    updateWaypointMarkers() {
        // Clear existing waypoint markers
        if (this.layers.waypoints) {
            this.map.removeLayer(this.layers.waypoints);
        }

        if (this.waypoints.length === 0) {
            this.layers.waypoints = null;
            return;
        }

        const markers = this.waypoints.map((waypoint, index) => {
            const isStart = index === 0;
            const isEnd = index === this.waypoints.length - 1 && this.waypoints.length > 1;
            
            let html, className;
            if (isStart) {
                html = '🟢';
                className = 'waypoint-marker start-waypoint';
            } else if (isEnd) {
                html = '🔴';
                className = 'waypoint-marker end-waypoint';
            } else {
                html = `<div style="background:#4285F4;color:white;border-radius:50%;width:24px;height:24px;display:flex;align-items:center;justify-content:center;border:2px solid white;font-size:12px;font-weight:bold;">${index + 1}</div>`;
                className = 'waypoint-marker intermediate-waypoint';
            }

            const marker = L.marker([waypoint.latitude, waypoint.longitude], {
                icon: L.divIcon({
                    html,
                    className,
                    iconSize: [24, 24],
                    iconAnchor: [12, 24]
                }),
                draggable: true
            });

            marker.bindPopup(`
                <div style="min-width:150px;">
                    <strong>${waypoint.name}</strong><br>
                    ${waypoint.latitude.toFixed(6)}, ${waypoint.longitude.toFixed(6)}<br>
                    <button onclick="window.removeWaypoint?.(${index})" style="margin-top:5px;padding:2px 8px;background:#ff4444;color:white;border:none;border-radius:3px;cursor:pointer;">Remove</button>
                </div>
            `);

            // Handle dragging
            marker.on('dragend', (e) => {
                const newPos = e.target.getLatLng();
                this.waypoints[index].latitude = newPos.lat;
                this.waypoints[index].longitude = newPos.lng;
                
                if (this.routePlanningCallbacks.onWaypointChanged) {
                    this.routePlanningCallbacks.onWaypointChanged(index, this.waypoints[index]);
                }
            });

            return marker;
        });

        this.layers.waypoints = L.layerGroup(markers);
        this.layers.waypoints.addTo(this.map);

        // Set global function for popup buttons
        window.removeWaypoint = (index) => {
            this.waypoints.splice(index, 1);
            this.updateWaypointMarkers();
            if (this.routePlanningCallbacks.onWaypointRemoved) {
                this.routePlanningCallbacks.onWaypointRemoved(index);
            }
        };
    }

    showRoute(routeData) {
        this.clearRoute();

        if (!routeData || !routeData.geometry || routeData.geometry.length === 0) {
            return;
        }

        // Convert route points to Leaflet format
        const latLngs = routeData.geometry.map(point => [point.latitude, point.longitude]);

        // Create main route line
        const routeLine = L.polyline(latLngs, {
            color: '#2196F3',
            weight: 5,
            opacity: 0.8,
            smoothFactor: 1.0
        });

        this.layers.plannedRoute = routeLine;
        this.layers.plannedRoute.addTo(this.map);

        // Fit map to route bounds if specified
        if (routeData.bounds) {
            this.fitBounds(
                routeData.bounds.minLatitude,
                routeData.bounds.maxLatitude,
                routeData.bounds.minLongitude,
                routeData.bounds.maxLongitude
            );
        } else {
            // Fallback: fit to route line bounds
            this.map.fitBounds(routeLine.getBounds(), { padding: [20, 20] });
        }
    }

    clearRoute() {
        if (this.layers.plannedRoute) {
            this.map.removeLayer(this.layers.plannedRoute);
            this.layers.plannedRoute = null;
        }
    }

    clearDirections() {
        if (this.layers.directions) {
            this.map.removeLayer(this.layers.directions);
            this.layers.directions = null;
        }
    }

    showDirections(directions) {
        // For now, we'll just log the directions
        // In a full implementation, you might show turn-by-turn markers
        console.log('Directions:', directions);

        // Clear any previous direction markers before adding new ones
        this.clearDirections();

        // Add direction markers to the map
        if (directions && directions.length > 0) {
            directions.forEach((direction, index) => {
                if (direction.location) {
                    const marker = L.circleMarker([direction.location.latitude, direction.location.longitude], {
                        radius: 4,
                        color: '#FF9800',
                        fillColor: '#FF9800',
                        fillOpacity: 0.8
                    });
                    
                    const dist = direction.distance < 1000
                        ? `${Math.round(direction.distance)} m`
                        : `${(direction.distance / 1000).toFixed(direction.distance < 10000 ? 1 : 0)} km`;

                    marker.bindPopup(`
                        <div style="min-width:200px;">
                            <strong>Step ${index + 1}</strong><br>
                            ${direction.instruction}<br>
                            <small>${dist}</small>
                        </div>
                    `);
                    
                    if (!this.layers.directions) {
                        this.layers.directions = L.layerGroup();
                        this.layers.directions.addTo(this.map);
                    }
                    
                    this.layers.directions.addLayer(marker);
                }
            });
    }
    }

    // Reset planning-related state for a fresh start
    resetRoutePlanning() {
        this.clearWaypoints();
        this.clearRoute();
        this.clearDirections();
        // Keep callbacks registered; enable/disable handled separately
    }

    fitBounds(minLat, maxLat, minLng, maxLng) {
        const bounds = L.latLngBounds([minLat, minLng], [maxLat, maxLng]);
        this.map.fitBounds(bounds, { padding: [20, 20] });
    }

    setRoutePlanningCallbacks(callbacks) {
        // Accept either plain JS function callbacks or a Blazor DotNetObjectReference
        if (!callbacks) {
            this.routePlanningCallbacks = {};
            return;
        }

        if (callbacks.dotNetRef) {
            const dot = callbacks.dotNetRef;
            // Wrap into JS functions that proxy to .NET methods
            this.routePlanningCallbacks = {
                onWaypointAdded: (wp) => {
                    try {
                        return dot.invokeMethodAsync('OnWaypointAdded', wp.latitude, wp.longitude, wp.name);
                    } catch (e) { /* no-op */ }
                },
                onWaypointChanged: (index, wp) => {
                    try {
                        return dot.invokeMethodAsync('OnWaypointChanged', index, wp.latitude, wp.longitude, wp.name);
                    } catch (e) { /* optional */ }
                },
                onWaypointRemoved: (index) => {
                    try {
                        return dot.invokeMethodAsync('OnWaypointRemoved', index);
                    } catch (e) { /* optional */ }
                }
            };
        } else {
            this.routePlanningCallbacks = callbacks || {};
        }
    }

    setMapCenter(lat, lng, zoom = 13) {
        this.map.setView([lat, lng], zoom);
    }
}

// Build API exposed to .NET
function buildApi(cm, elementId) {
    return {
        addRouteData: (...args) => {
            const coords = (args.length === 1 && Array.isArray(args[0])) ? args[0] : args;
            cm.addRouteData(coords);
        },
        setTileLayer: (layerType) => cm.setTileLayer(layerType),
        toggleClustering: () => cm.toggleClustering(),
        showElevationProfile: () => cm.showElevationProfile(),
        enableRoutePlanning: (enabled) => cm.enableRoutePlanning(enabled),
        addWaypoint: (lat, lng, name) => cm.addWaypoint(lat, lng, name),
        clearWaypoints: () => cm.clearWaypoints(),
        getWaypoints: () => cm.getWaypoints(),
        showRoute: (routeData) => cm.showRoute(routeData),
        clearRoute: () => cm.clearRoute(),
    clearDirections: () => cm.clearDirections(),
    resetRoutePlanning: () => cm.resetRoutePlanning(),
        showDirections: (directions) => cm.showDirections(directions),
        fitBounds: (minLat, maxLat, minLng, maxLng) => cm.fitBounds(minLat, maxLat, minLng, maxLng),
        setMapCenter: (lat, lng, zoom) => cm.setMapCenter(lat, lng, zoom),
        setRoutePlanningCallbacks: (callbacks) => cm.setRoutePlanningCallbacks(callbacks),
        dispose: () => { cm.dispose(); instanceApis.delete(elementId); }
    };
}

// Exported init (idempotent & concurrency-safe)
export async function initializeMap(elementId, centerLat, centerLon, config) {
    const el = document.getElementById(elementId);
    if (!el) throw new Error(`Map container #${elementId} not found`);

    if (instanceApis.has(elementId)) return instanceApis.get(elementId);
    if (instancePromises.has(elementId)) return instancePromises.get(elementId);

    const p = (async () => {
        await loadLeaflet();

        if (instanceApis.has(elementId)) return instanceApis.get(elementId);

        const cm = new CyclingMap(elementId, centerLat, centerLon, config || {});
        const api = buildApi(cm, elementId);
        instanceApis.set(elementId, api);
        return api;
    })();

    instancePromises.set(elementId, p);
    try {
        return await p;
    } finally {
        instancePromises.delete(elementId);
    }
}
