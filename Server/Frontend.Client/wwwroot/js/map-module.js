let leafletLoaded = false;
let leafletLoadPromise = null;

// Keep one CyclingMap/API per elementId, and dedupe concurrent inits
const instanceApis = new Map();        // elementId -> API object
const instancePromises = new Map();    // elementId -> Promise<API>

// CDN URLs
const CDN_URLS = {
    leafletCss: 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css',
    leafletJs: 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js',           // classic script
    markerClusterCss: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.css',
    markerClusterDefaultCss: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/MarkerCluster.Default.css',
    markerClusterJs: 'https://unpkg.com/leaflet.markercluster@1.5.3/dist/leaflet.markercluster.js', // classic
    elevationCss: 'https://unpkg.com/@raruto/leaflet-elevation@2.2.1/dist/leaflet-elevation.css',
    elevationEsm: 'https://unpkg.com/@raruto/leaflet-elevation@2.2.1/dist/leaflet-elevation.min.js', // ES module
    d3: 'https://unpkg.com/d3@7.8.5/dist/d3.min.js' // classic, exposed as window.d3 (helps elevation)
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
        // CSS
        await Promise.all([
            loadCSS(CDN_URLS.leafletCss),
            loadCSS(CDN_URLS.markerClusterCss),
            loadCSS(CDN_URLS.markerClusterDefaultCss),
            loadCSS(CDN_URLS.elevationCss)
        ]);

        // Leaflet classic -> defines window.L (extensible)
        await loadScript(CDN_URLS.leafletJs);
        if (!window.L) throw new Error('Leaflet failed to load');

        // MarkerCluster classic -> augments window.L (needs extensible L)
        await loadScript(CDN_URLS.markerClusterJs);

        // D3 classic (some builds of elevation expect global d3)
        await loadScript(CDN_URLS.d3);

        // Elevation as **ES module** (so its internal relative imports resolve)
        await import(CDN_URLS.elevationEsm);

        leafletLoaded = true;
        console.log('✅ Leaflet and cycling plugins loaded successfully');
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

// Elevation profile
function createElevationProfile(map, coordinates) {
    if (!window.L.control || !window.L.control.elevation) {
        console.warn('Elevation plugin not loaded');
        return null;
    }
    const ctrl = L.control.elevation({
        position: 'bottomright',
        theme: 'steelblue-theme',
        detached: true,
        elevationDiv: '#elevation-profile',
        width: Math.min(350, window.innerWidth - 40), 
        height: 150,
        margins: { top: 20, right: 30, bottom: 30, left: 50 },
        imperial: false,
        summary: 'inline'
    }).addTo(map);

    const geojson = {
        type: 'LineString',
        coordinates: coordinates.map(p => [p.longitude, p.latitude, p.elevation ?? 0])
    };
    ctrl.addData(geojson);
    return ctrl;
}

// Map wrapper
class CyclingMap {
    constructor(elementId, centerLat, centerLon, config) {
        this.elementId = elementId;
        this.config = config ?? {};
        this.map = null;
        this.layers = { route: null, markers: null, elevation: null };
        this.currentTileLayer = null;
        this.coordinates = [];

        this.initializeMap(centerLat, centerLon);
        this.addCustomCSS();
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
        position:absolute; 
        bottom:80px; 
        right:20px; 
        background:#fff; 
        border-radius:5px; 
        box-shadow:0 2px 10px rgba(0,0,0,.3); 
        padding:10px; 
        z-index:1000; 
        max-width:90vw; 
      }
      
      /* Mobile responsive styles */
      @media (max-width:768px) { 
        .cycling-popup { min-width:150px; max-width:250px; }
        .cycling-popup div { font-size:12px; }
        
        .leaflet-bottom .leaflet-left .map-controls { 
          margin-bottom: 8px; 
          margin-left: 8px; 
          gap: 6px; 
        }
        
        .map-control-btn { 
          padding:6px 10px; 
          font-size:11px; 
          min-width:90px; 
        }
        
        #elevation-profile { 
          bottom:70px; 
          right:10px; 
          left:10px; 
          width:auto; 
          max-width:none; 
        }
      }
      
      @media (max-width:480px) { 
        .cycling-popup { min-width:120px; max-width:200px; }
        .cycling-popup div { font-size:11px; }
        
        .leaflet-bottom .leaflet-left .map-controls { 
          margin-bottom: 6px; 
          margin-left: 6px; 
          gap: 4px; 
          flex-direction: column;
          align-items: flex-start;
        }
        
        .map-control-btn { 
          padding:4px 8px; 
          font-size:10px; 
          min-width:80px; 
        }
        
        .start-marker, .end-marker { font-size:14px; }
        
        #elevation-profile { 
          bottom:60px; 
          right:5px; 
          left:5px; 
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
        // Set initial state based on config (now defaults to false)
        clusterBtn.innerHTML = this.config.EnableClustering === true ? '📍 Clustering ON' : '📍 Clustering OFF';
        clusterBtn.title = 'Toggle marker clustering';
        if (this.config.EnableClustering === true) {
            clusterBtn.classList.add('active');
        }
        L.DomEvent.on(clusterBtn, 'click', () => {
            this.toggleClustering();
        });

        const elevationBtn = L.DomUtil.create('button', 'map-control-btn', controlDiv);
        elevationBtn.innerHTML = '📊 Show Elevation';
        elevationBtn.title = 'Show elevation profile';
        L.DomEvent.on(elevationBtn, 'click', () => {
            this.showElevationProfile();
        });

        const Custom = L.Control.extend({ onAdd: () => controlDiv });
        new Custom({ position: 'bottomleft' }).addTo(this.map);
    }

    addRouteData(coordinates) {
        // Accept array or varargs
        const coords = Array.isArray(coordinates) ? coordinates : Array.from(arguments);

        this.coordinates = coords.map(c => ({
            latitude: c.latitude,
            longitude: c.longitude,
            elevation: c.elevation ?? c.elevationGain ?? 0, // Handle both field names  
            speed: c.speed ?? c.currentSpeed ?? 0,         // Handle both field names
            temperature: c.temperature ?? c.currentTemperature ?? 20, // Handle both field names
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
        const clusterBtn = document.querySelector('.map-control-btn[title="Toggle marker clustering"]');
        if (clusterBtn) {
            if (this.config.EnableClustering) {
                clusterBtn.classList.add('active');
                clusterBtn.innerHTML = '📍 Clustering ON';
            } else {
                clusterBtn.classList.remove('active'); 
                clusterBtn.innerHTML = '📍 Clustering OFF';
            }
        }
    }

    showElevationProfile() {
        if (this.coordinates.length === 0) return;

        let div = document.getElementById('elevation-profile');
        if (!div) {
            div = document.createElement('div');
            div.id = 'elevation-profile';
            div.style.display = 'none';
            document.body.appendChild(div);
        }

        const isCurrentlyVisible = div.style.display !== 'none';
        
        if (!isCurrentlyVisible && !this.layers.elevation) {
            this.layers.elevation = createElevationProfile(this.map, this.coordinates);
        }
        
        // Toggle visibility
        div.style.display = isCurrentlyVisible ? 'none' : 'block';
        
        // Update button state to reflect CURRENT state (not inverted)
        const elevationBtn = document.querySelector('.map-control-btn[title="Show elevation profile"]');
        if (elevationBtn) {
            if (div.style.display === 'block') {
                elevationBtn.classList.add('active');
                elevationBtn.innerHTML = '📊 Hide Elevation';
            } else {
                elevationBtn.classList.remove('active');
                elevationBtn.innerHTML = '📊 Show Elevation';
            }
        }
    }

    clearLayers() {
        for (const k of Object.keys(this.layers)) {
            if (this.layers[k]) {
                this.map.removeLayer(this.layers[k]);
                this.layers[k] = null;
            }
        }
    }

    dispose() {
        this.clearLayers();
        if (this.map) { this.map.remove(); this.map = null; }
        const div = document.getElementById('elevation-profile');
        if (div) div.remove();
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
